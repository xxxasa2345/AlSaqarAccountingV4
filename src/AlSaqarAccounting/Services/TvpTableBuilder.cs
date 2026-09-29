using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Builds TVP DataTables whose columns exactly match the live SQL Server
/// table-type definition (name, ordinal and CLR type discovered from
/// sys.columns). Model values are matched case-insensitively by property
/// name, so an insert can never fail because of a column mismatch between
/// the forensic model and the server type. Columns declared NOT NULL in the
/// type receive a sensible default instead of DBNull.
/// </summary>
internal static class TvpTableBuilder
{
    private static readonly ConcurrentDictionary<string, Task<List<TvpColumn>>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static async Task<DataTable> CreateAsync(
        DbExecutor db,
        string typeName,
        IEnumerable<object> rows,
        CancellationToken cancellationToken = default)
    {
        var shape = await GetShapeAsync(db, typeName, cancellationToken).ConfigureAwait(false);

        var table = new DataTable(typeName);
        foreach (var column in shape)
            table.Columns.Add(column.Name, column.ClrType);

        var propertiesCache = new Dictionary<Type, PropertyInfo[]>();
        var values = new object?[shape.Count];

        foreach (var model in rows)
        {
            var modelType = model.GetType();
            if (!propertiesCache.TryGetValue(modelType, out var properties))
            {
                properties = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                propertiesCache[modelType] = properties;
            }

            for (var i = 0; i < shape.Count; i++)
            {
                var property = properties.FirstOrDefault(p =>
                    string.Equals(p.Name, shape[i].Name, StringComparison.OrdinalIgnoreCase));

                var value = property?.GetValue(model);
                values[i] = value is null || value is DBNull
                    ? shape[i].DefaultForNull()
                    : value;
            }

            table.Rows.Add(values.ToArray()!);
        }

        return table;
    }

    private static Task<List<TvpColumn>> GetShapeAsync(
        DbExecutor db,
        string typeName,
        CancellationToken cancellationToken)
        => Cache.GetOrAdd(typeName, name => LoadShapeAsync(db, name, cancellationToken));

    private static async Task<List<TvpColumn>> LoadShapeAsync(
        DbExecutor db,
        string typeName,
        CancellationToken cancellationToken)
    {
        var table = await db.QueryAsync(
            @"
SELECT c.name AS ColumnName,
       tp.name AS TypeName,
       c.is_nullable AS IsNullable
FROM sys.table_types AS tt
INNER JOIN sys.columns AS c
        ON c.object_id = tt.type_table_object_id
INNER JOIN sys.types AS tp
        ON tp.user_type_id = c.user_type_id
WHERE SCHEMA_NAME(tt.schema_id) = 'dbo'
      AND tt.name = @TypeName
ORDER BY c.column_id;",
            p => p.Add("@TypeName", System.Data.SqlClient.SqlDbType.NVarChar, 128).Value = typeName,
            cancellationToken).ConfigureAwait(false);

        if (table.Rows.Count == 0)
            throw new InvalidOperationException(
                $"نوع الجدول dbo.{typeName} غير موجود في قاعدة البيانات.");

        var shape = new List<TvpColumn>(table.Rows.Count);
        foreach (DataRow row in table.Rows)
        {
            var columnName = Convert.ToString(row["ColumnName"]) ?? string.Empty;
            var sqlType = Convert.ToString(row["TypeName"]) ?? "nvarchar";
            var isNullable = Convert.ToInt32(row["IsNullable"]) == 1;
            shape.Add(new TvpColumn(columnName, ClrTypeOf(sqlType), isNullable, sqlType));
        }

        return shape;
    }

    private static Type ClrTypeOf(string sqlType) => sqlType.Trim().ToLowerInvariant() switch
    {
        "bigint" => typeof(long),
        "int" => typeof(int),
        "smallint" => typeof(short),
        "tinyint" => typeof(byte),
        "bit" => typeof(bool),
        "decimal" or "numeric" or "money" or "smallmoney" => typeof(decimal),
        "float" => typeof(double),
        "real" => typeof(float),
        "date" or "datetime" or "datetime2" or "smalldatetime" => typeof(DateTime),
        "datetimeoffset" => typeof(DateTimeOffset),
        "uniqueidentifier" => typeof(Guid),
        "varbinary" or "binary" or "image" => typeof(byte[]),
        _ => typeof(string)
    };

    private sealed record TvpColumn(string Name, Type ClrType, bool IsNullable, string SqlType)
    {
        public object DefaultForNull() => !IsNullable
            ? ClrType == typeof(string) ? string.Empty
              : ClrType == typeof(DateTime) ? DateTime.Now
              : Activator.CreateInstance(ClrType) ?? string.Empty
            : DBNull.Value;
    }
}
