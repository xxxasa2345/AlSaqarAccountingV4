using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Resolves a screen to an actual SQL-backed source at runtime and provides
/// schema-driven CRUD for tables whose columns permit it. Specialized ERP
/// Forms remain preferred; this service is the concrete last-mile implementation
/// for the remaining User_Screens records, not the old placeholder screen.
/// </summary>
public sealed class DynamicErpScreenService
{
    private readonly DbExecutor _db;

    public DynamicErpScreenService(DbExecutor db) => _db = db;

    public async Task<DynamicErpDefinition> ResolveAsync(
        string screenName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(screenName))
            throw new ArgumentException("اسم الشاشة مطلوب.", nameof(screenName));

        var table = ScreenEntityMap.Resolve(screenName);
        if (!string.IsNullOrWhiteSpace(table) && await TableExistsAsync(table, cancellationToken))
            return await BuildTableDefinitionAsync(screenName.Trim(), table, cancellationToken);

        var inferredTable = await FindTableByScreenNameAsync(screenName.Trim(), cancellationToken);
        if (!string.IsNullOrWhiteSpace(inferredTable))
            return await BuildTableDefinitionAsync(screenName.Trim(), inferredTable!, cancellationToken);

        var procedure = await FindSelectProcedureAsync(screenName.Trim(), cancellationToken);
        if (!string.IsNullOrWhiteSpace(procedure.Name))
            return new DynamicErpDefinition(
                screenName.Trim(),
                null,
                procedure.Name,
                procedure.NeedsBranch,
                null,
                Array.Empty<DynamicErpColumn>());

        throw new InvalidOperationException(
            $"لم يتم العثور على مصدر قاعدة بيانات معروف للشاشة «{screenName.Trim()}». " +
            "يجب إضافة خريطة الشاشة الأصلية قبل تنفيذها.");
    }

    public Task<DataTable> LoadAsync(
        DynamicErpDefinition definition,
        int? branchId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(definition.TableName))
        {
            var table = QuoteIdentifier(definition.TableName!);
            return _db.QueryAsync(
                $"SELECT TOP (5000) * FROM dbo.{table} ORDER BY {QuoteIdentifier(definition.KeyColumn ?? definition.Columns.FirstOrDefault()?.Name ?? "1")};",
                cancellationToken: cancellationToken);
        }

        return _db.ExecuteStoredProcedureAsync(
            definition.ListProcedure!,
            p =>
            {
                if (definition.ListProcedureNeedsBranch)
                {
                    if (!branchId.HasValue)
                        throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");
                    p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
                }
            },
            cancellationToken);
    }

    public async Task SaveAsync(
        DynamicErpDefinition definition,
        IReadOnlyDictionary<string, object?> values,
        int? key,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(definition.TableName))
            throw new InvalidOperationException("هذه الشاشة مرتبطة بإجراء قراءة فقط، وليس بجدول CRUD مباشر.");

        var writable = definition.Columns.Where(c => c.IsWritable).ToArray();
        if (writable.Length == 0)
            throw new InvalidOperationException("لا توجد أعمدة قابلة للتحرير في هذا الكيان.");

        using var cn = new SqlConnection(_db.ConnectionString);
        await cn.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = cn.CreateCommand();
        cmd.CommandTimeout = 120;

        if (key.HasValue && !string.IsNullOrWhiteSpace(definition.KeyColumn))
        {
            var assignments = new List<string>();
            foreach (var column in writable)
            {
                var parameterName = "@v_" + column.Name.Replace("[", "").Replace("]", "").Replace(".", "_");
                assignments.Add($"{QuoteIdentifier(column.Name)}={parameterName}");
                cmd.Parameters.Add(parameterName, ToSqlDbType(column.SqlType), column.MaxLength).Value =
                    ToDbValue(GetValue(values, column.Name));
            }

            AddAuditUpdateParameters(cmd, session, writable);
            cmd.CommandText =
                $"UPDATE dbo.{QuoteIdentifier(definition.TableName!)} SET {string.Join(", ", assignments)} " +
                $"WHERE {QuoteIdentifier(definition.KeyColumn!)}=@__id;";
            cmd.Parameters.Add("@__id", SqlDbType.Int).Value = key.Value;
        }
        else
        {
            var columns = new List<string>();
            var parameters = new List<string>();
            foreach (var column in writable)
            {
                var parameterName = "@v_" + column.Name.Replace("[", "").Replace("]", "").Replace(".", "_");
                columns.Add(QuoteIdentifier(column.Name));
                parameters.Add(parameterName);
                cmd.Parameters.Add(parameterName, ToSqlDbType(column.SqlType), column.MaxLength).Value =
                    ToDbValue(GetValue(values, column.Name));
            }

            AddAuditInsertParameters(cmd, session, writable);
            foreach (var audit in AuditColumns(writable, "add"))
            {
                columns.Add(QuoteIdentifier(audit.Column));
                parameters.Add(audit.Parameter);
            }

            cmd.CommandText =
                $"INSERT INTO dbo.{QuoteIdentifier(definition.TableName!)} ({string.Join(", ", columns)}) " +
                $"VALUES ({string.Join(", ", parameters)});";
        }

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(
        DynamicErpDefinition definition,
        int key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(definition.TableName) || string.IsNullOrWhiteSpace(definition.KeyColumn))
            throw new InvalidOperationException("هذا الكيان لا يدعم حذفاً عاماً آمناً.");

        return _db.ExecuteAsync(
            $"DELETE FROM dbo.{QuoteIdentifier(definition.TableName!)} WHERE {QuoteIdentifier(definition.KeyColumn!)}=@ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = key,
            cancellationToken);
    }

    private async Task<DynamicErpDefinition> BuildTableDefinitionAsync(
        string screenName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var table = await _db.QueryAsync(
            @"
SELECT c.column_id,
       c.name AS ColumnName,
       t.name AS SqlType,
       c.max_length,
       c.is_nullable,
       c.is_identity,
       c.is_computed,
       CONVERT(bit, CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END) AS IsPrimaryKey
FROM sys.tables AS tb
INNER JOIN sys.columns AS c ON c.object_id = tb.object_id
INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
LEFT JOIN (
    SELECT ic.object_id, ic.column_id
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
    WHERE i.is_primary_key = 1
) pk ON pk.object_id=tb.object_id AND pk.column_id=c.column_id
WHERE SCHEMA_NAME(tb.schema_id)='dbo'
  AND tb.name=@TableName
ORDER BY c.column_id;",
            p => p.Add("@TableName", SqlDbType.NVarChar, 256).Value = tableName,
            cancellationToken).ConfigureAwait(false);

        if (table.Rows.Count == 0)
            throw new InvalidOperationException($"الجدول dbo.{tableName} غير موجود.");

        var columns = new List<DynamicErpColumn>(table.Rows.Count);
        string? key = null;
        foreach (DataRow row in table.Rows)
        {
            var name = Convert.ToString(row["ColumnName"]) ?? string.Empty;
            var sqlType = Convert.ToString(row["SqlType"]) ?? "nvarchar";
            var maxLength = Convert.ToInt32(row["max_length"]);
            if (sqlType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) ||
                sqlType.Equals("nchar", StringComparison.OrdinalIgnoreCase))
                maxLength = maxLength < 0 ? -1 : Math.Max(1, maxLength / 2);

            var primary = Convert.ToBoolean(row["IsPrimaryKey"]);
            if (primary && key is null)
                key = name;

            columns.Add(new DynamicErpColumn(
                name,
                sqlType,
                maxLength,
                Convert.ToBoolean(row["is_nullable"]),
                Convert.ToBoolean(row["is_identity"]),
                Convert.ToBoolean(row["is_computed"]),
                primary));
        }

        return new DynamicErpDefinition(
            screenName,
            tableName,
            null,
            definitionNeedsBranch(columns),
            key,
            columns);
    }

    private async Task<(string? Name, bool NeedsBranch)> FindSelectProcedureAsync(
        string screenName,
        CancellationToken cancellationToken)
    {
        var names = new List<string>();
        var n = screenName.Trim();
        names.Add(n);

        if (n.StartsWith("Frm", StringComparison.OrdinalIgnoreCase))
        {
            var shortName = n.Substring(3);
            names.Add(shortName);
            names.Add(shortName.Replace("Form", ""));
        }

        foreach (var candidate in names.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var table = await _db.QueryAsync(
                @"
SELECT TOP (1)
       p.name,
       CONVERT(bit, CASE WHEN EXISTS (
           SELECT 1
           FROM sys.parameters sp
           WHERE sp.object_id=p.object_id
             AND sp.name='@BranchID'
       ) THEN 1 ELSE 0 END) AS NeedsBranch
FROM sys.procedures AS p
WHERE p.name IN (@n1,@n2,@n3,@n4,@n5)
ORDER BY CASE
    WHEN name=@n1 THEN 1
    WHEN name=@n2 THEN 2
    WHEN name=@n3 THEN 3
    WHEN name=@n4 THEN 4
    ELSE 5 END;",
                p =>
                {
                    p.Add("@n1", SqlDbType.NVarChar, 256).Value = "Select_" + candidate;
                    p.Add("@n2", SqlDbType.NVarChar, 256).Value = "Select_Order_" + candidate;
                    p.Add("@n3", SqlDbType.NVarChar, 256).Value = "Select_Account_" + candidate;
                    p.Add("@n4", SqlDbType.NVarChar, 256).Value = "Select_Item_" + candidate;
                    p.Add("@n5", SqlDbType.NVarChar, 256).Value = "Select_Search_" + candidate;
                },
                cancellationToken).ConfigureAwait(false);

            if (table.Rows.Count > 0)
                return (
                    Convert.ToString(table.Rows[0][0]),
                    Convert.ToBoolean(table.Rows[0]["NeedsBranch"]));
        }

        return (null, false);
    }

    private async Task<string?> FindTableByScreenNameAsync(
        string screenName,
        CancellationToken cancellationToken)
    {
        var value = screenName.Trim();
        if (value.StartsWith("Frm", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(3);
        if (value.EndsWith("Form", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(0, value.Length - 4);

        var candidates = new[]
        {
            value,
            "Item_" + value,
            "Account_" + value,
            "Contract_" + value,
            "Order_" + value,
            "Orders_" + value,
            "Emp_" + value,
            "Repairs_" + value,
            "Restaurant_" + value,
            "Scaffold_" + value,
            "Virg_" + value
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var exact = await _db.QueryAsync(
                "SELECT TOP (1) name FROM sys.tables WHERE SCHEMA_NAME(schema_id)='dbo' AND name=@Name;",
                p => p.Add("@Name", SqlDbType.NVarChar, 256).Value = candidate,
                cancellationToken).ConfigureAwait(false);

            if (exact.Rows.Count == 1)
                return Convert.ToString(exact.Rows[0]["name"]);

            var contains = await _db.QueryAsync(
                @"SELECT TOP (2) name
                  FROM sys.tables
                  WHERE SCHEMA_NAME(schema_id)='dbo'
                    AND name LIKE @Pattern
                  ORDER BY name;",
                p => p.Add("@Pattern", SqlDbType.NVarChar, 300).Value = "%" + candidate + "%",
                cancellationToken).ConfigureAwait(false);

            if (contains.Rows.Count == 1)
                return Convert.ToString(contains.Rows[0]["name"]);
        }

        return null;
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        var data = await _db.QueryAsync(
            "SELECT 1 FROM sys.tables WHERE SCHEMA_NAME(schema_id)='dbo' AND name=@Name;",
            p => p.Add("@Name", SqlDbType.NVarChar, 256).Value = tableName,
            cancellationToken).ConfigureAwait(false);
        return data.Rows.Count > 0;
    }

    private static bool definitionNeedsBranch(IEnumerable<DynamicErpColumn> columns)
        => columns.Any(c => c.Name.Equals("BranchID", StringComparison.OrdinalIgnoreCase));

    private static object? GetValue(IReadOnlyDictionary<string, object?> values, string name)
        => values.TryGetValue(name, out var value) ? value : null;

    private static object ToDbValue(object? value) => value is null ? DBNull.Value : value;

    private static string QuoteIdentifier(string identifier)
        => "[" + identifier.Replace("]", "]]") + "]";

    private static SqlDbType ToSqlDbType(string sqlType) => sqlType.ToLowerInvariant() switch
    {
        "bigint" => SqlDbType.BigInt,
        "int" => SqlDbType.Int,
        "smallint" => SqlDbType.SmallInt,
        "tinyint" => SqlDbType.TinyInt,
        "bit" => SqlDbType.Bit,
        "decimal" => SqlDbType.Decimal,
        "numeric" => SqlDbType.Decimal,
        "money" => SqlDbType.Money,
        "smallmoney" => SqlDbType.SmallMoney,
        "float" => SqlDbType.Float,
        "real" => SqlDbType.Real,
        "date" => SqlDbType.Date,
        "datetime" => SqlDbType.DateTime,
        "datetime2" => SqlDbType.DateTime2,
        "smalldatetime" => SqlDbType.SmallDateTime,
        "uniqueidentifier" => SqlDbType.UniqueIdentifier,
        "varbinary" => SqlDbType.VarBinary,
        "binary" => SqlDbType.Binary,
        _ => SqlDbType.NVarChar
    };

    private static void AddAuditInsertParameters(
        SqlCommand cmd,
        AppSession session,
        IEnumerable<DynamicErpColumn> columns)
    {
        if (Has(columns, "UserID_Add")) cmd.Parameters.Add("@__uid", SqlDbType.Int).Value = session.UserId;
        if (Has(columns, "UserBranch_Add")) cmd.Parameters.Add("@__ub", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
        if (Has(columns, "UserMacAddress_Add")) cmd.Parameters.Add("@__umac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
        if (Has(columns, "UserDate_Add")) cmd.Parameters.Add("@__udate", SqlDbType.DateTime).Value = DateTime.Now;
    }

    private static IEnumerable<(string Column, string Parameter)> AuditColumns(
        IReadOnlyCollection<DynamicErpColumn> columns, string mode)
    {
        if (mode != "add") yield break;
        if (Has(columns, "UserID_Add")) yield return ("UserID_Add", "@__uid");
        if (Has(columns, "UserBranch_Add")) yield return ("UserBranch_Add", "@__ub");
        if (Has(columns, "UserMacAddress_Add")) yield return ("UserMacAddress_Add", "@__umac");
        if (Has(columns, "UserDate_Add")) yield return ("UserDate_Add", "@__udate");
    }

    private static void AddAuditUpdateParameters(
        SqlCommand cmd,
        AppSession session,
        IEnumerable<DynamicErpColumn> columns)
    {
        if (Has(columns, "UserID_Update")) cmd.Parameters.Add("@__uidu", SqlDbType.Int).Value = session.UserId;
        if (Has(columns, "UserBranch_Update")) cmd.Parameters.Add("@__ubu", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
        if (Has(columns, "UserMacAddress_Update")) cmd.Parameters.Add("@__umacu", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
        if (Has(columns, "UserDate_Update")) cmd.Parameters.Add("@__udateu", SqlDbType.DateTime).Value = DateTime.Now;
    }

    private static bool Has(IEnumerable<DynamicErpColumn> columns, string name)
        => columns.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool Has(DynamicErpColumn[] columns, string name)
        => columns.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public sealed record DynamicErpDefinition(
    string ScreenName,
    string? TableName,
    string? ListProcedure,
    bool ListProcedureNeedsBranch,
    string? KeyColumn,
    IReadOnlyList<DynamicErpColumn> Columns);

public sealed record DynamicErpColumn(
    string Name,
    string SqlType,
    int MaxLength,
    bool IsNullable,
    bool IsIdentity,
    bool IsComputed,
    bool IsPrimaryKey)
{
    public bool IsAudit => Name.StartsWith("User", StringComparison.OrdinalIgnoreCase);
    public bool IsWritable =>
        !IsIdentity &&
        !IsComputed &&
        !IsPrimaryKey &&
        !IsAudit &&
        !Name.Equals("YearId", StringComparison.OrdinalIgnoreCase);
}
