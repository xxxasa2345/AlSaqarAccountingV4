using System.Data;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Accounting operations.
/// Handles chart of accounts, journal entries, and general ledger operations.
/// </summary>
public sealed class AccountingService
{
    private readonly DbExecutor _db;

    public AccountingService(DbExecutor db) => _db = db;

    #region Chart of Accounts

    public Task<DataTable> GetChartOfAccountsAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var sql = branchId.HasValue
            ? "SELECT * FROM dbo.Account_Accounts WHERE BranchID = @BranchID OR BranchID IS NULL ORDER BY Account_No"
            : "SELECT * FROM dbo.Account_Accounts ORDER BY Account_No";
        
        return branchId.HasValue
            ? _db.QueryAsync(sql, p => p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value, cancellationToken)
            : _db.QueryAsync(sql, cancellationToken: cancellationToken);
    }

    public Task<DataTable> SearchAccountsAsync(string searchTerm, int? branchId = null, CancellationToken cancellationToken = default)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
            return GetChartOfAccountsAsync(branchId, cancellationToken);

        var sql = branchId.HasValue
            ? @"SELECT * FROM dbo.Account_Accounts 
               WHERE (BranchID = @BranchID OR BranchID IS NULL)
                 AND (Account_Name LIKE '%' + @Search + '%' 
                      OR Account_No LIKE '%' + @Search + '%' 
                      OR E_Account_Name LIKE '%' + @Search + '%')
               ORDER BY Account_No"
            : @"SELECT * FROM dbo.Account_Accounts 
               WHERE Account_Name LIKE '%' + @Search + '%' 
                 OR Account_No LIKE '%' + @Search + '%' 
                 OR E_Account_Name LIKE '%' + @Search + '%'
               ORDER BY Account_No";

        return _db.QueryAsync(sql, p =>
        {
            p.Add("@Search", SqlDbType.NVarChar, 100).Value = term;
            if (branchId.HasValue)
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
        }, cancellationToken);
    }

    public async Task<int> CreateAccountAsync(
        int accountNo,
        string accountName,
        string eAccountName,
        int? accountType,
        int? accountNature,
        int? mainAccountNo,
        int? accountLevel,
        int? branchId,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO dbo.Account_Accounts (
    Account_No, Account_Name, E_Account_Name, Account_Type, Account_Nature,
    Main_Account_No, Account_Level, BranchID,
    UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add
)
VALUES (
    @Account_No, @Account_Name, @E_Account_Name, @Account_Type, @Account_Nature,
    @Main_Account_No, @Account_Level, @BranchID,
    @UserID_Add, @UserBranch_Add, @UserMacAddress_Add, GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var result = await _db.QueryAsync(sql, p =>
        {
            p.Add("@Account_No", SqlDbType.Int).Value = accountNo;
            p.Add("@Account_Name", SqlDbType.NVarChar, 300).Value = accountName.Trim();
            p.Add("@E_Account_Name", SqlDbType.NVarChar, 300).Value = (object?)eAccountName ?? DBNull.Value;
            p.Add("@Account_Type", SqlDbType.Int).Value = (object?)accountType ?? DBNull.Value;
            p.Add("@Account_Nature", SqlDbType.Int).Value = (object?)accountNature ?? DBNull.Value;
            p.Add("@Main_Account_No", SqlDbType.Int).Value = (object?)mainAccountNo ?? DBNull.Value;
            p.Add("@Account_Level", SqlDbType.Int).Value = (object?)accountLevel ?? DBNull.Value;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
            p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Add", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);

        return result.Rows.Count > 0 ? Convert.ToInt32(result.Rows[0][0]) : 0;
    }

    public async Task<int> UpdateAccountAsync(
        int id,
        int accountNo,
        string accountName,
        string eAccountName,
        int? accountType,
        int? accountNature,
        int? mainAccountNo,
        int? accountLevel,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE dbo.Account_Accounts
SET 
    Account_No = @Account_No,
    Account_Name = @Account_Name,
    E_Account_Name = @E_Account_Name,
    Account_Type = @Account_Type,
    Account_Nature = @Account_Nature,
    Main_Account_No = @Main_Account_No,
    Account_Level = @Account_Level,
    UserID_Update = @UserID_Update,
    UserBranch_Update = @UserBranch_Update,
    UserMacAddress_Update = @UserMacAddress_Update,
    UserDate_Update = GETDATE()
WHERE ID = @ID;";

        return await _db.ExecuteAsync(sql, p =>
        {
            p.Add("@ID", SqlDbType.Int).Value = id;
            p.Add("@Account_No", SqlDbType.Int).Value = accountNo;
            p.Add("@Account_Name", SqlDbType.NVarChar, 300).Value = accountName.Trim();
            p.Add("@E_Account_Name", SqlDbType.NVarChar, 300).Value = (object?)eAccountName ?? DBNull.Value;
            p.Add("@Account_Type", SqlDbType.Int).Value = (object?)accountType ?? DBNull.Value;
            p.Add("@Account_Nature", SqlDbType.Int).Value = (object?)accountNature ?? DBNull.Value;
            p.Add("@Main_Account_No", SqlDbType.Int).Value = (object?)mainAccountNo ?? DBNull.Value;
            p.Add("@Account_Level", SqlDbType.Int).Value = (object?)accountLevel ?? DBNull.Value;
            p.Add("@UserID_Update", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Update", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Update", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);
    }

    public async Task<int> DeleteAccountAsync(int id, CancellationToken cancellationToken = default)
    {
        var children = await _db.QueryAsync(
            "SELECT COUNT(*) FROM dbo.Account_Accounts WHERE Main_Account_No = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken).ConfigureAwait(false);

        if (Convert.ToInt32(children.Rows[0][0]) > 0)
            throw new InvalidOperationException("لا يمكن حذف حساب له حسابات فرعية.");

        return await _db.ExecuteAsync(
            "DELETE FROM dbo.Account_Accounts WHERE ID = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Cost Centers

    public Task<DataTable> GetCostCentersAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var sql = branchId.HasValue
            ? "SELECT * FROM dbo.Account_CostCenters WHERE BranchID = @BranchID OR BranchID IS NULL ORDER BY CostCentersName"
            : "SELECT * FROM dbo.Account_CostCenters ORDER BY CostCentersName";
        
        return branchId.HasValue
            ? _db.QueryAsync(sql, p => p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value, cancellationToken)
            : _db.QueryAsync(sql, cancellationToken: cancellationToken);
    }

    public async Task<int> CreateCostCenterAsync(
        int costCenterNo,
        string costCenterName,
        int? mainCostCenterId,
        int? branchId,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO dbo.Account_CostCenters (
    CostCentersNo, CostCentersName, MainCostCentersID, BranchID,
    UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add
)
VALUES (
    @CostCentersNo, @CostCentersName, @MainCostCentersID, @BranchID,
    @UserID_Add, @UserBranch_Add, @UserMacAddress_Add, GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var result = await _db.QueryAsync(sql, p =>
        {
            p.Add("@CostCentersNo", SqlDbType.Int).Value = costCenterNo;
            p.Add("@CostCentersName", SqlDbType.NVarChar, 300).Value = costCenterName.Trim();
            p.Add("@MainCostCentersID", SqlDbType.Int).Value = (object?)mainCostCenterId ?? DBNull.Value;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
            p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Add", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);

        return result.Rows.Count > 0 ? Convert.ToInt32(result.Rows[0][0]) : 0;
    }

    #endregion

    #region Account Balance

    public Task<DataTable> GetAccountBalanceAsync(int accountId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var sql = @"
SELECT
    a.ID,
    a.Account_No,
    a.Account_Name,
    ISNULL(SUM(d.Debit), 0) AS TotalDebit,
    ISNULL(SUM(d.Credit), 0) AS TotalCredit,
    ISNULL(SUM(d.Debit), 0) - ISNULL(SUM(d.Credit), 0) AS Balance
FROM dbo.Account_Accounts a
LEFT JOIN dbo.Tran_TranDetails d ON d.Account_Sn = a.ID
LEFT JOIN dbo.Tran_Tran h ON h.ID = d.TranSn AND h.BranchID = d.BranchID
WHERE a.ID = @AccountId";

        if (fromDate.HasValue)
            sql += " AND h.TranDate >= @FromDate";
        if (toDate.HasValue)
            sql += " AND h.TranDate < DATEADD(DAY, 1, @ToDate)";

        sql += " GROUP BY a.ID, a.Account_No, a.Account_Name";

        return _db.QueryAsync(sql, p =>
        {
            p.Add("@AccountId", SqlDbType.Int).Value = accountId;
            if (fromDate.HasValue)
                p.Add("@FromDate", SqlDbType.DateTime).Value = fromDate.Value;
            if (toDate.HasValue)
                p.Add("@ToDate", SqlDbType.DateTime).Value = toDate.Value;
        }, cancellationToken);
    }

    #endregion

    #region Journal Entries

    public async Task<int> CreateJournalEntryAsync(
        DateTime entryDate,
        string description,
        int branchId,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO dbo.Account_Receipts (
    Note, UserDate_Add, UserID_Add, UserBranch_Add, UserMacAddress_Add
)
VALUES (
    @Note, @EntryDate, @UserID_Add, @UserBranch_Add, @UserMacAddress_Add
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var result = await _db.QueryAsync(sql, p =>
        {
            p.Add("@Note", SqlDbType.NVarChar, 1000).Value = description.Trim();
            p.Add("@EntryDate", SqlDbType.DateTime).Value = entryDate;
            p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Add", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);

        return result.Rows.Count > 0 ? Convert.ToInt32(result.Rows[0][0]) : 0;
    }

    #endregion

    #region Trial Balance

    public Task<DataTable> GetTrialBalanceAsync(DateTime? fromDate = null, DateTime? toDate = null, int? branchId = null, CancellationToken cancellationToken = default)
    {
        var sql = @"
SELECT
    a.ID,
    a.Account_No,
    a.Account_Name,
    ISNULL(SUM(d.Debit), 0) AS TotalDebit,
    ISNULL(SUM(d.Credit), 0) AS TotalCredit,
    ISNULL(SUM(d.Debit), 0) - ISNULL(SUM(d.Credit), 0) AS Balance
FROM dbo.Account_Accounts a
LEFT JOIN dbo.Tran_TranDetails d ON d.Account_Sn = a.ID
LEFT JOIN dbo.Tran_Tran h ON h.ID = d.TranSn AND h.BranchID = d.BranchID
WHERE a.Account_No IS NOT NULL";

        if (branchId.HasValue)
            sql += " AND (d.BranchID = @BranchID OR d.BranchID IS NULL)";
        if (fromDate.HasValue)
            sql += " AND h.TranDate >= @FromDate";
        if (toDate.HasValue)
            sql += " AND h.TranDate < DATEADD(DAY, 1, @ToDate)";

        sql += " GROUP BY a.ID, a.Account_No, a.Account_Name ORDER BY a.Account_No";

        return _db.QueryAsync(sql, p =>
        {
            if (branchId.HasValue)
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            if (fromDate.HasValue)
                p.Add("@FromDate", SqlDbType.DateTime).Value = fromDate.Value;
            if (toDate.HasValue)
                p.Add("@ToDate", SqlDbType.DateTime).Value = toDate.Value;
        }, cancellationToken);
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
