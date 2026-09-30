using System.Data;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

public sealed class AuthService
{
    private readonly SqlConnectionFactory _factory;

    public AuthService(SqlConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Reads the real users/branches stored in GTSdb2026.
    /// The original GTS login screen presents a user selector and a branch selector.
    /// </summary>
    public async Task<DataTable> GetActiveUsersAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ID,
                Name,
                BranchID,
                GroupID,
                IsActive
            FROM dbo.User_Login
            WHERE ISNULL(IsActive, 1) = 1
            ORDER BY
                ISNULL(BranchID, 2147483647),
                Name,
                ID;
            """;

        return await QueryTableAsync(sql, null, cancellationToken);
    }

    public async Task<DataTable> GetBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ID,
                Name
            FROM dbo.Account_Branch
            ORDER BY ID;
            """;

        return await QueryTableAsync(sql, null, cancellationToken);
    }

    /// <summary>
    /// Authenticates exactly against the selected User_Login record and branch.
    /// This mirrors the original flow where Class_Login.LOGIN receives UserID
    /// and password and the branch is part of the login context.
    /// </summary>
    public async Task<AppSession?> LoginAsync(
        int userId,
        int branchId,
        string password,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (1)
                ID,
                Name,
                BranchID,
                GroupID,
                IsActive,
                IsOpenDay
            FROM dbo.User_Login
            WHERE ID = @UserID
              AND ISNULL(BranchID, @BranchID) = @BranchID
              AND PassWord = @PassWord
              AND ISNULL(IsActive, 1) = 1
            ORDER BY ID;
            """;

        using var cn = _factory.Create();
        using var cmd = new SqlCommand(sql, cn)
        {
            CommandTimeout = 60
        };

        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
        cmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = branchId;
        cmd.Parameters.Add("@PassWord", SqlDbType.NVarChar, -1).Value = password ?? string.Empty;

        await cn.OpenAsync(cancellationToken);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var session = new AppSession
        {
            UserId = reader.GetInt32(0),
            UserName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            BranchId = reader.IsDBNull(2) ? branchId : reader.GetInt32(2),
            GroupId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            IsOpenDay = !reader.IsDBNull(5) && reader.GetBoolean(5)
        };

        return session;
    }

    /// <summary>
    /// Writes the same operational audit information used by the original login:
    /// user, MAC address, application version and database version.
    /// Logging failure must never prevent a valid user from entering the ERP.
    /// </summary>
    public async Task RecordLoginAsync(
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (session.BranchId is null)
            return;

        try
        {
            var mac = GetMacAddress();
            var appVersion = typeof(AuthService).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";
            var databaseVersion = await GetDatabaseVersionAsync(session.BranchId.Value, cancellationToken);

            const string sql = """
                INSERT INTO dbo.LoginLogs
                    (UserID, UserLogsDate, MacAddressLogs, AppVersion, DatabaseVersion)
                VALUES
                    (@UserID, @UserLogsDate, @MacAddressLogs, @AppVersion, @DatabaseVersion);
                """;

            using var cn = _factory.Create();
            using var cmd = new SqlCommand(sql, cn)
            {
                CommandTimeout = 60
            };

            cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = session.UserId;
            cmd.Parameters.Add("@UserLogsDate", SqlDbType.DateTime).Value = DateTime.Now;
            cmd.Parameters.Add("@MacAddressLogs", SqlDbType.NVarChar, 200).Value = mac;
            cmd.Parameters.Add("@AppVersion", SqlDbType.NVarChar, 200).Value = appVersion;
            cmd.Parameters.Add("@DatabaseVersion", SqlDbType.NVarChar, 200).Value =
                (object?)databaseVersion ?? DBNull.Value;

            await cn.OpenAsync(cancellationToken);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Audit logging is best-effort and must not block a valid login.
        }
    }

    private async Task<string?> GetDatabaseVersionAsync(
        int branchId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) DatabaseVersion
            FROM dbo.TblSetting
            WHERE ID = @BranchID;
            """;

        using var cn = _factory.Create();
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = branchId;

        await cn.OpenAsync(cancellationToken);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? null : value.ToString();
    }

    private async Task<DataTable> QueryTableAsync(
        string sql,
        Action<SqlParameterCollection>? addParameters,
        CancellationToken cancellationToken)
    {
        using var cn = _factory.Create();
        using var cmd = new SqlCommand(sql, cn)
        {
            CommandTimeout = 60
        };

        addParameters?.Invoke(cmd.Parameters);

        await cn.OpenAsync(cancellationToken);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private static string GetMacAddress()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            var address = nic.GetPhysicalAddress().ToString();
            if (!string.IsNullOrWhiteSpace(address))
                return address;
        }

        return string.Empty;
    }
}
