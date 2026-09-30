using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for the chart of accounts. Reads through the original
/// Get_Account_Tree procedure of GTSdb2026, and maintains accounts with fully
/// parameterized statements whose columns come from the forensic schema
/// export only (Account_Accounts). The form knows nothing about SQL.
/// </summary>
public sealed class AccountsTreeService
{
    private readonly DbExecutor _db;

    public AccountsTreeService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync("dbo.Get_Account_Tree", cancellationToken: cancellationToken);

    /// <summary>Flat chart-of-accounts list used by account pickers.</summary>
    public Task<DataTable> ListAccountsAsync(CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync("dbo.Select_SearchAccount", cancellationToken: cancellationToken);

    /// <summary>Creates a child account under an existing parent account.
    /// The account number is computed exactly like the original system:
    /// MAX(child number) + 1 under the parent, or parent*10+1 for the first child.</summary>
    public async Task<int> CreateAccountAsync(
        int parentAccountNo,
        string name,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (parentAccountNo <= 0)
            throw new ArgumentException("يجب اختيار حساب أب صحيح.");
        var accountName = name?.Trim() ?? string.Empty;
        if (accountName.Length is < 1 or > 300)
            throw new ArgumentException("اسم الحساب يجب أن يكون بين 1 و300 حرف.");

        var parent = await _db.QueryAsync(
            @"
SELECT Account_No, Main_Account_No, Account_Level, Final_Account, Account_Type, Account_Nature, BranchID
FROM dbo.Account_Accounts
WHERE Account_No = @Parent;",
            p => p.Add("@Parent", SqlDbType.Int).Value = parentAccountNo,
            cancellationToken).ConfigureAwait(false);

        if (parent.Rows.Count == 0)
            throw new InvalidOperationException($"لا يوجد حساب أب بالرقم {parentAccountNo}.");

        var finalAccountRaw = parent.Rows[0]["Final_Account"];
        if (finalAccountRaw is not DBNull && Convert.ToInt32(finalAccountRaw) == 1)
            throw new InvalidOperationException("لا يمكن إضافة حساب فرعي تحت حساب فرعي — اختر حساباً رئيسياً.");

        var parentRow = parent.Rows[0];
        var accountLevel = parentRow["Account_Level"] is DBNull ? 1 : Convert.ToInt32(parentRow["Account_Level"]);
        var accountType = parentRow["Account_Type"] is DBNull ? (object?)null : Convert.ToInt32(parentRow["Account_Type"]);
        var accountNature = parentRow["Account_Nature"] is DBNull ? (object?)null : Convert.ToInt32(parentRow["Account_Nature"]);
        var parentBranch = parentRow["BranchID"] is DBNull ? (int?)null : Convert.ToInt32(parentRow["BranchID"]);

        var nextNumberTable = await _db.QueryAsync(
            @"
SELECT ISNULL(
           (SELECT MAX(Account_No) FROM dbo.Account_Accounts WHERE Main_Account_No = @Parent),
           @Parent * 10)
       + 1;",
            p => p.Add("@Parent", SqlDbType.Int).Value = parentAccountNo,
            cancellationToken).ConfigureAwait(false);
        var accountNo = Convert.ToInt32(nextNumberTable.Rows[0][0]);

        var branchId = session.BranchId ?? parentBranch;

        await InsertAccountRowAsync(
            accountNo,
            parentAccountNo,
            accountLevel + 1,
            accountType,
            accountNature,
            accountName,
            branchId,
            session,
            insertId: true,
            cancellationToken).ConfigureAwait(false);

        return accountNo;
    }

    /// <summary>Renames an existing account and stamps the update audit columns.</summary>
    public Task<int> UpdateAccountNameAsync(
        int accountNo,
        string name,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        var accountName = name?.Trim() ?? string.Empty;
        if (accountName.Length is < 1 or > 300)
            throw new ArgumentException("اسم الحساب يجب أن يكون بين 1 و300 حرف.");

        return _db.ExecuteAsync(
            @"
UPDATE dbo.Account_Accounts
SET Account_Name = @Name,
    UserID_Update = @UserID,
    UserBranch_Update = @BranchID,
    UserMacAddress_Update = @Mac,
    UserDate_Update = GETDATE()
WHERE Account_No = @AccountNo;",
            p =>
            {
                p.Add("@Name", SqlDbType.NVarChar, 300).Value = accountName;
                p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                p.Add("@AccountNo", SqlDbType.Int).Value = accountNo;
            },
            cancellationToken);
    }

    /// <summary>Deletes a leaf account only. Accounts with children are rejected.</summary>
    public async Task DeleteAccountAsync(int accountNo, CancellationToken cancellationToken = default)
    {
        var children = await _db.QueryAsync(
            "SELECT COUNT(*) FROM dbo.Account_Accounts WHERE Main_Account_No = @AccountNo;",
            p => p.Add("@AccountNo", SqlDbType.Int).Value = accountNo,
            cancellationToken).ConfigureAwait(false);

        if (Convert.ToInt32(children.Rows[0][0]) > 0)
            throw new InvalidOperationException("لا يمكن حذف حساب له حسابات فرعية — احذف الفرعية أولاً.");

        await _db.ExecuteAsync(
            "DELETE FROM dbo.Account_Accounts WHERE Account_No = @AccountNo;",
            p => p.Add("@AccountNo", SqlDbType.Int).Value = accountNo,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The default parent account number under which new customers
    /// are created (Account_DefualtCustomer.AccountID).</summary>
    public async Task<int> GetDefaultCustomerParentAsync(
        int? branchId,
        CancellationToken cancellationToken = default)
    {
        var table = await _db.QueryAsync(
            @"
SELECT TOP 1 AccountID
FROM dbo.Account_DefualtCustomer
WHERE (@BranchID IS NULL OR BranchID = @BranchID)
ORDER BY SN;",
            p => p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value,
            cancellationToken).ConfigureAwait(false);

        if (table.Rows.Count == 0 || table.Rows[0]["AccountID"] is DBNull)
            throw new InvalidOperationException(
                "لم يُعثر على الحساب الافتراضي للعملاء (Account_DefualtCustomer).");

        return Convert.ToInt32(table.Rows[0]["AccountID"]);
    }

    /// <summary>
    /// The ID column is an identity in most GTSdb2026 exports. If the server
    /// rejects the identity-style insert because ID is not identity (error
    /// 515), the row is inserted again with an explicitly computed ID.
    /// </summary>
    private async Task InsertAccountRowAsync(
        int accountNo,
        int parentAccountNo,
        int accountLevel,
        object? accountType,
        object? accountNature,
        string accountName,
        int? branchId,
        AppSession session,
        bool insertId,
        CancellationToken cancellationToken)
    {
        const string identitySql = @"
INSERT INTO dbo.Account_Accounts
    (Account_No, Suspended, Account_Level, Final_Account, Account_Type, Account_Nature,
     Main_Account_No, BranchID, Account_Name, Priv_Debit, Priv_Credit,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
VALUES
    (@AccountNo, 0, @Level, 1, @Type, @Nature, @Parent, @BranchID, @Name, 0, 0,
     @UserID, @BranchID, @Mac, GETDATE());";

        const string computedIdSql = @"
INSERT INTO dbo.Account_Accounts
    (ID, Account_No, Suspended, Account_Level, Final_Account, Account_Type, Account_Nature,
     Main_Account_No, BranchID, Account_Name, Priv_Debit, Priv_Credit,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
SELECT
    (SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.Account_Accounts),
    @AccountNo, 0, @Level, 1, @Type, @Nature, @Parent, @BranchID, @Name, 0, 0,
    @UserID, @BranchID, @Mac, GETDATE();";

        try
        {
            await _db.ExecuteAsync(
                insertId ? identitySql : computedIdSql,
                p => BuildInsertParameters(
                    p, accountNo, parentAccountNo, accountLevel, accountType, accountNature,
                    accountName, branchId, session),
                cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (insertId && ex.Number == 515)
        {
            await _db.ExecuteAsync(
                computedIdSql,
                p => BuildInsertParameters(
                    p, accountNo, parentAccountNo, accountLevel, accountType, accountNature,
                    accountName, branchId, session),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static void BuildInsertParameters(
        SqlParameterCollection p,
        int accountNo,
        int parentAccountNo,
        int accountLevel,
        object? accountType,
        object? accountNature,
        string accountName,
        int? branchId,
        AppSession session)
    {
        p.Add("@AccountNo", SqlDbType.Int).Value = accountNo;
        p.Add("@Level", SqlDbType.Int).Value = accountLevel;
        p.Add("@Type", SqlDbType.Int).Value = accountType ?? DBNull.Value;
        p.Add("@Nature", SqlDbType.Int).Value = accountNature ?? DBNull.Value;
        p.Add("@Parent", SqlDbType.Int).Value = parentAccountNo;
        p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
        p.Add("@Name", SqlDbType.NVarChar, 300).Value = accountName;
        p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
        p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
    }
}
