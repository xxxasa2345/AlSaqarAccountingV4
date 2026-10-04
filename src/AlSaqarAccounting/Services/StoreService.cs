using System.Data;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Store management (Account_Stores).
/// </summary>
public sealed class StoreService
{
    private readonly DbExecutor _db;

    public StoreService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var sql = branchId.HasValue
            ? "SELECT * FROM dbo.Account_Stores WHERE BranchID = @BranchID OR BranchID IS NULL ORDER BY Store_Name"
            : "SELECT * FROM dbo.Account_Stores ORDER BY Store_Name";
        
        return branchId.HasValue
            ? _db.QueryAsync(sql, p => p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value, cancellationToken)
            : _db.QueryAsync(sql, cancellationToken: cancellationToken);
    }

    public Task<DataTable> SearchAsync(string searchTerm, int? branchId = null, CancellationToken cancellationToken = default)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
            return ListAsync(branchId, cancellationToken);

        var sql = branchId.HasValue
            ? @"SELECT * FROM dbo.Account_Stores 
               WHERE (BranchID = @BranchID OR BranchID IS NULL)
                 AND (Store_Name LIKE '%' + @Search + '%' 
                      OR Phone LIKE '%' + @Search + '%' 
                      OR Address LIKE '%' + @Search + '%')
               ORDER BY Store_Name"
            : @"SELECT * FROM dbo.Account_Stores 
               WHERE Store_Name LIKE '%' + @Search + '%' 
                 OR Phone LIKE '%' + @Search + '%' 
                 OR Address LIKE '%' + @Search + '%'
               ORDER BY Store_Name";

        return _db.QueryAsync(sql, p =>
        {
            p.Add("@Search", SqlDbType.NVarChar, 100).Value = term;
            if (branchId.HasValue)
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
        }, cancellationToken);
    }

    public async Task<int> CreateAsync(Account_Stores store, AppSession session, CancellationToken cancellationToken = default)
    {
        Validate(store);

        const string sql = @"
INSERT INTO dbo.Account_Stores (
    Store_Name, BranchID, Address, Phone, Fax,
    UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add
)
VALUES (
    @Store_Name, @BranchID, @Address, @Phone, @Fax,
    @UserID_Add, @UserBranch_Add, @UserMacAddress_Add, GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var result = await _db.QueryAsync(sql, p =>
        {
            p.Add("@Store_Name", SqlDbType.NVarChar, 200).Value = store.Store_Name.Trim();
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)store.BranchID ?? DBNull.Value;
            p.Add("@Address", SqlDbType.NVarChar, 500).Value = (object?)store.Address ?? DBNull.Value;
            p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)store.Phone ?? DBNull.Value;
            p.Add("@Fax", SqlDbType.NVarChar, 100).Value = (object?)store.Fax ?? DBNull.Value;
            p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Add", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);

        if (result.Rows.Count == 0)
            throw new InvalidOperationException("لم يتم إنشاء المخزن"); // "لم يتم إنشاء المخزن"

        return Convert.ToInt32(result.Rows[0][0]);
    }

    public async Task<int> UpdateAsync(Account_Stores store, AppSession session, CancellationToken cancellationToken = default)
    {
        Validate(store);

        const string sql = @"
UPDATE dbo.Account_Stores
SET 
    Store_Name = @Store_Name,
    BranchID = @BranchID,
    Address = @Address,
    Phone = @Phone,
    Fax = @Fax,
    UserID_Update = @UserID_Update,
    UserBranch_Update = @UserBranch_Update,
    UserMacAddress_Update = @UserMacAddress_Update,
    UserDate_Update = GETDATE()
WHERE ID = @ID;";

        return await _db.ExecuteAsync(sql, p =>
        {
            p.Add("@ID", SqlDbType.Int).Value = store.ID;
            p.Add("@Store_Name", SqlDbType.NVarChar, 200).Value = store.Store_Name.Trim();
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)store.BranchID ?? DBNull.Value;
            p.Add("@Address", SqlDbType.NVarChar, 500).Value = (object?)store.Address ?? DBNull.Value;
            p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)store.Phone ?? DBNull.Value;
            p.Add("@Fax", SqlDbType.NVarChar, 100).Value = (object?)store.Fax ?? DBNull.Value;
            p.Add("@UserID_Update", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Update", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Update", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);
    }

    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync("DELETE FROM dbo.Account_Stores WHERE ID = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id, cancellationToken);

    public Task<Account_Stores?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.QuerySingleAsync<Account_Stores>(
            "SELECT * FROM dbo.Account_Stores WHERE ID = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id, cancellationToken);

    private static void Validate(Account_Stores store)
    {
        if (string.IsNullOrWhiteSpace(store.Store_Name))
            throw new ArgumentException("اسم المخزن مطلوب"); // "اسم المخزن مطلوب"
        if (store.Store_Name.Trim().Length > 200)
            throw new ArgumentException(" 200 "); // "اسم المخزن طويل جدا"
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
