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

    public async Task<DataTable> ExecuteStoredProcedureAsync(
        string procedureName,
        Action<SqlParameterCollection>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var cn = _factory.Create();
        using var cmd = new SqlCommand(procedureName, cn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        parameters?.Invoke(cmd.Parameters);
        await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public async Task<int> ExecuteStoredProcedureNonQueryAsync(
        string procedureName,
        Action<SqlParameterCollection>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var cn = _factory.Create();
        using var cmd = new SqlCommand(procedureName, cn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        parameters?.Invoke(cmd.Parameters);
        await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> QuerySingleAsync<T>(
        string sql,
        Action<SqlParameterCollection>? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new()
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
        
        if (!reader.HasRows)
            return null;
        
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var obj = new T();
        var props = typeof(T).GetProperties();
        
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var fieldName = reader.GetName(i);
            var prop = props.FirstOrDefault(p => 
                string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));
            
            if (prop != null && !reader.IsDBNull(i))
            {
                try
                {
                    prop.SetValue(obj, reader.GetValue(i));
                }
                catch { /* Ignore conversion errors */ }
            }
        }
        
        return obj;
    }
}
