using System.Data;
using System.Data.SqlClient;

namespace AlSaqarAccounting.Core;

public sealed class StoredProcedureExecutor
{
    private readonly SqlConnectionFactory _factory;
    public StoredProcedureExecutor(SqlConnectionFactory factory) => _factory = factory;

    public async Task<DataTable> QueryAsync(
        string procedureName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var cn = _factory.Create();
        using var cmd = new SqlCommand(procedureName, cn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };

        if (parameters is not null)
        {
            foreach (var pair in parameters)
                cmd.Parameters.AddWithValue(pair.Key, pair.Value ?? DBNull.Value);
        }

        await cn.OpenAsync(cancellationToken);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public async Task<int> ExecuteAsync(
        string procedureName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var cn = _factory.Create();
        using var cmd = new SqlCommand(procedureName, cn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };

        if (parameters is not null)
            foreach (var pair in parameters)
                cmd.Parameters.AddWithValue(pair.Key, pair.Value ?? DBNull.Value);

        await cn.OpenAsync(cancellationToken);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
