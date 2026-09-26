using System.Data;
using Microsoft.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

public sealed class AuthService
{
    private readonly SqlConnectionFactory _factory;
    public AuthService(SqlConnectionFactory factory) => _factory = factory;

    // Uses only the exact User_Login columns discovered in GTSdb2026.
    // Password verification is performed by equality against the existing column;
    // if the original application hashes/transforms it, replace this method with
    // the original procedure once its definition is exported.
    public async Task<AppSession?> LoginAsync(
        string userName, string password, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (1)
                ID, Name, BranchID, GroupID, IsActive, IsOpenDay
            FROM dbo.User_Login
            WHERE Name = @Name
              AND PassWord = @PassWord
              AND ISNULL(IsActive, 1) = 1
            ORDER BY ID;
            """;

        await using var cn = _factory.Create();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 150).Value = userName;
        cmd.Parameters.Add("@PassWord", SqlDbType.NVarChar, -1).Value = password;

        await cn.OpenAsync(cancellationToken);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await r.ReadAsync(cancellationToken))
            return null;

        return new AppSession
        {
            UserId = r.GetInt32(0),
            UserName = r.IsDBNull(1) ? "" : r.GetString(1),
            BranchId = r.IsDBNull(2) ? null : r.GetInt32(2),
            GroupId = r.IsDBNull(3) ? null : r.GetInt32(3),
            IsOpenDay = !r.IsDBNull(5) && r.GetBoolean(5)
        };
    }
}
