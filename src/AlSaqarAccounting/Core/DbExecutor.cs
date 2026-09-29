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

    /// <summary>
    /// Runs a stored procedure and returns the value of one OUTPUT parameter
    /// after execution (used by dbo.Insert_Tran_Tran which returns the new
    /// voucher serial through @Transn OUTPUT).
    /// </summary>
    public async Task<object?> ExecuteStoredProcedureOutputAsync(
        string procedureName,
        Action<SqlParameterCollection> parameters,
        string outputParameterName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputParameterName))
            throw new ArgumentException("اسم بارامتر الإخراج مطلوب.", nameof(outputParameterName));

        using var cn = _factory.Create();
        using var cmd = new SqlCommand(procedureName, cn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        parameters.Invoke(cmd.Parameters);
        await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var parameter = cmd.Parameters.Cast<SqlParameter>().FirstOrDefault(p =>
            string.Equals(p.ParameterName, outputParameterName.Trim(), StringComparison.OrdinalIgnoreCase));
        return parameter?.Value;
    }
}
