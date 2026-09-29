using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Live stored-procedure contract discovered from sys.parameters at runtime.
/// The catalog CSV truncates long signatures, so instead of guessing the full
/// parameter list we read it from the database itself and bind every parameter
/// BY NAME (ADO.NET binds procedure parameters by name, never by ordinal).
/// Parameters we do not map keep their server default, or receive DBNull when
/// the server declares them without a default.
/// </summary>
internal sealed class StoredProcedureContract
{
    private static readonly ConcurrentDictionary<string, Task<StoredProcedureContract>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<ContractParameter> _parameters;
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _procedureName;

    private StoredProcedureContract(string procedureName, List<ContractParameter> parameters)
    {
        _procedureName = procedureName;
        _parameters = parameters;
    }

    public static Task<StoredProcedureContract> LoadAsync(
        DbExecutor db,
        string procedureName,
        CancellationToken cancellationToken = default)
        => Cache.GetOrAdd(procedureName, name => LoadCoreAsync(db, name, cancellationToken));

    private static async Task<StoredProcedureContract> LoadCoreAsync(
        DbExecutor db,
        string procedureName,
        CancellationToken cancellationToken)
    {
        var table = await db.QueryAsync(
            @"
SELECT p.name AS ParameterName,
       tp.name AS TypeName,
       p.max_length AS MaxLength,
       p.is_output AS IsOutput,
       p.has_default_value AS HasDefault
FROM sys.parameters AS p
INNER JOIN sys.types AS tp
        ON tp.user_type_id = p.user_type_id
WHERE p.object_id = OBJECT_ID(@Procedure)
ORDER BY p.parameter_id;",
            p => p.Add("@Procedure", SqlDbType.NVarChar, 400).Value = procedureName,
            cancellationToken).ConfigureAwait(false);

        if (table.Rows.Count == 0)
            throw new InvalidOperationException(
                $"الإجراء {procedureName} غير موجود في قاعدة البيانات.");

        var parameters = new List<ContractParameter>(table.Rows.Count);
        foreach (DataRow row in table.Rows)
        {
            parameters.Add(new ContractParameter(
                Name: (Convert.ToString(row["ParameterName"]) ?? string.Empty).Trim(),
                TypeName: (Convert.ToString(row["TypeName"]) ?? string.Empty).Trim(),
                MaxLength: Convert.ToInt16(row["MaxLength"]),
                IsOutput: Convert.ToInt32(row["IsOutput"]) == 1,
                HasDefault: Convert.ToInt32(row["HasDefault"]) == 1));
        }

        return new StoredProcedureContract(procedureName, parameters);
    }

    /// <summary>Sets a parameter value by name. Throws for unknown names so a
    /// typo can never silently corrupt a save.</summary>
    public StoredProcedureContract Set(string name, object? value)
    {
        if (!_parameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                $"البارامتر {name} غير موجود في {_procedureName}.");

        _values[name] = value;
        return this;
    }

    /// <summary>Sets a parameter only when the procedure declares it.</summary>
    public StoredProcedureContract SetIfPresent(string name, object? value)
    {
        if (_parameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            _values[name] = value;
        return this;
    }

    public Action<SqlParameterCollection> BuildParameters()
    {
        var snapshot = new Dictionary<string, object?>(_values, StringComparer.OrdinalIgnoreCase);
        return p =>
        {
            foreach (var parameter in _parameters)
            {
                if (parameter.IsOutput)
                    continue;

                if (snapshot.TryGetValue(parameter.Name, out var value))
                {
                    AddParameter(p, parameter, value);
                    continue;
                }

                if (!parameter.HasDefault)
                    AddParameter(p, parameter, DBNull.Value);
            }
        };
    }

    private static void AddParameter(SqlParameterCollection p, ContractParameter parameter, object? value)
    {
        if (IsTableType(parameter.TypeName))
        {
            var tvp = p.Add(parameter.Name, SqlDbType.Structured);
            tvp.TypeName = "dbo." + parameter.TypeName;
            tvp.Value = value ?? DBNull.Value;
            return;
        }

        var sqlType = SqlTypeOf(parameter.TypeName);
        if (sqlType is SqlDbType.NVarChar or SqlDbType.VarChar or SqlDbType.NChar or SqlDbType.Char)
        {
            var size = parameter.MaxLength;
            if (parameter.MaxLength == -1)
                size = -1;
            else if (sqlType == SqlDbType.NVarChar && size != -1)
                size /= 2;

            p.Add(parameter.Name, sqlType, size).Value = value ?? DBNull.Value;
            return;
        }

        p.Add(parameter.Name, sqlType).Value = value ?? DBNull.Value;
    }

    private static bool IsTableType(string typeName) =>
        typeName.Equals("Items_Orders", StringComparison.OrdinalIgnoreCase) ||
        typeName.Equals("Items_Purches", StringComparison.OrdinalIgnoreCase) ||
        typeName.Equals("Item_Items", StringComparison.OrdinalIgnoreCase);

    private static SqlDbType SqlTypeOf(string typeName) => typeName.Trim().ToLowerInvariant() switch
    {
        "int" => SqlDbType.Int,
        "bigint" => SqlDbType.BigInt,
        "smallint" => SqlDbType.SmallInt,
        "tinyint" => SqlDbType.TinyInt,
        "bit" => SqlDbType.Bit,
        "decimal" or "numeric" => SqlDbType.Decimal,
        "money" => SqlDbType.Money,
        "smallmoney" => SqlDbType.SmallMoney,
        "float" => SqlDbType.Float,
        "real" => SqlDbType.Real,
        "date" => SqlDbType.Date,
        "datetime" or "datetime2" or "smalldatetime" => SqlDbType.DateTime,
        "uniqueidentifier" => SqlDbType.UniqueIdentifier,
        "varchar" => SqlDbType.VarChar,
        "nchar" => SqlDbType.NChar,
        "char" => SqlDbType.Char,
        "text" => SqlDbType.Text,
        "ntext" => SqlDbType.NText,
        _ => SqlDbType.NVarChar
    };

    private sealed record ContractParameter(
        string Name,
        string TypeName,
        short MaxLength,
        bool IsOutput,
        bool HasDefault);
}
