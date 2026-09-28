using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

public sealed class SchemaService
{
    private readonly SqlConnectionFactory _factory;
    public SchemaService(SqlConnectionFactory factory) => _factory = factory;

    public async Task<DataTable> GetTablesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT s.name AS SchemaName,
                   t.name AS TableName,
                   SUM(CASE WHEN p.index_id IN (0,1) THEN p.rows ELSE 0 END) AS ApproxRows
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id=t.schema_id
            LEFT JOIN sys.partitions p ON p.object_id=t.object_id
            WHERE t.is_ms_shipped=0
            GROUP BY s.name,t.name
            ORDER BY s.name,t.name;
            """;
        using var cn = _factory.Create();
        using var cmd = new SqlCommand(sql, cn);
        await cn.OpenAsync(ct);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);
        return dt;
    }
}
