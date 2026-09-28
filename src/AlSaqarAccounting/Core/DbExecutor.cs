using System.Data;
using System.Data.SqlClient;

namespace AlSaqarAccounting.Core;

/// <summary>
/// Centralized SQL execution boundary. UI and business services should not
/// create SqlConnection/SqlCommand directly.
/// </summary>
public sealed class DbExecutor
{
    private readonly SqlConnectionFactory _factory;

    public DbExecutor(SqlConnectionFactory factory) => _factory = factory;

    public async Task<DataTable> QueryAsync(
        string sql,
        Action<SqlParameterCollection>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var cn = _factory.Create();
        using var cmd = new SqlCommand(sql, cn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 120
        };
        parameters?.Invoke(cmd.Parameters);
        await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public async Task<int> ExecuteAsync(
        string sql,
        Action<SqlParameterCollection>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var cn = _factory.Create();
        using var cmd = new SqlCommand(sql, cn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 120
        };
        parameters?.Invoke(cmd.Parameters);
        await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
