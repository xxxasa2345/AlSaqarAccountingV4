using System.Data;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Item_Unit. The form knows nothing about SQL.
/// </summary>
public sealed class ItemUnitService
{
    private readonly DbExecutor _db;

    public ItemUnitService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(
            "SELECT ID, Name, UserBranch_Add, UserDate_Add, UserDate_Update FROM dbo.Item_Unit ORDER BY Name, ID;",
            cancellationToken: cancellationToken);

    public async Task<int> CreateAsync(
        string name,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        const string sql = @"
INSERT INTO dbo.Item_Unit
    (Name, UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
VALUES
    (@Name, @UserID, @BranchID, @Mac, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS int);";

        // DbExecutor.ExecuteAsync is intentionally non-query; this insert is kept
        // here as a parameterized command through a dedicated query boundary.
        var table = await _db.QueryAsync(sql, p =>
        {
            p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
            p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@Mac", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);

        return Convert.ToInt32(table.Rows[0][0]);
    }

    public Task<int> UpdateAsync(
        int id,
        string name,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        const string sql = @"
UPDATE dbo.Item_Unit
SET Name = @Name,
    UserID_Update = @UserID,
    UserBranch_Update = @BranchID,
    UserMacAddress_Update = @Mac,
    UserDate_Update = GETDATE()
WHERE ID = @ID;";

        return _db.ExecuteAsync(sql, p =>
        {
            p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
            p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@Mac", SqlDbType.NVarChar, 200).Value = GetMachineMac();
            p.Add("@ID", SqlDbType.Int).Value = id;
        }, cancellationToken);
    }

    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync(
            "DELETE FROM dbo.Item_Unit WHERE ID = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);

    private static void ValidateName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 300)
            throw new ArgumentException("اسم الوحدة يجب أن يكون بين 1 و300 حرف.");
    }

    private static string GetMachineMac()
    {
        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Select(n => n.GetPhysicalAddress()?.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return string.IsNullOrWhiteSpace(mac) ? Environment.MachineName : mac;
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
