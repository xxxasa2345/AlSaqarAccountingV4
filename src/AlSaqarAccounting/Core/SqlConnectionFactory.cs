using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AlSaqarAccounting.Core;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
        => _connectionString = configuration.GetConnectionString("GtsDb2026")
            ?? throw new InvalidOperationException("ConnectionStrings:GtsDb2026 is missing.");

    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQL connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public string ConnectionString => _connectionString;

    public SqlConnection Create() => new(_connectionString);
}
