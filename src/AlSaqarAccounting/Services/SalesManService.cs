using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Account_SalesMan (مندوبو المبيعات). The primary
/// key is SN; the service falls back to a computed SN when the server rejects
/// an identity-style insert. All values are parameterized.
/// </summary>
public sealed class SalesManService
{
    private readonly DbExecutor _db;

    public SalesManService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(
            @"
SELECT SN, ID, Name, Phone, profit, ISProfitOrder, BranchID,
       UserDate_Add, UserDate_Update
FROM dbo.Account_SalesMan
ORDER BY Name, SN;",
            cancellationToken: cancellationToken);

    public Task<int> CreateAsync(
        string name,
        string? phone,
        decimal? profitPercent,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        const string identitySql = @"
INSERT INTO dbo.Account_SalesMan
    (Name, Phone, profit, BranchID,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
VALUES
    (@Name, @Phone, @Profit, @BranchID,
     @UserID, @BranchID, @Mac, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS int);";

        const string computedSnSql = @"
INSERT INTO dbo.Account_SalesMan
    (SN, Name, Phone, profit, BranchID,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
SELECT
    (SELECT ISNULL(MAX(SN), 0) + 1 FROM dbo.Account_SalesMan),
    @Name, @Phone, @Profit, @BranchID,
    @UserID, @BranchID, @Mac, GETDATE();
SELECT (SELECT ISNULL(MAX(SN), 0) FROM dbo.Account_SalesMan);";

        return ExecuteInsertWithFallbackAsync(identitySql, computedSnSql, name, phone, profitPercent, session, cancellationToken);
    }

    public Task<int> UpdateAsync(
        int sn,
        string name,
        string? phone,
        decimal? profitPercent,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        return _db.ExecuteAsync(
            @"
UPDATE dbo.Account_SalesMan
SET Name = @Name,
    Phone = @Phone,
    profit = @Profit,
    UserID_Update = @UserID,
    UserBranch_Update = @BranchID,
    UserMacAddress_Update = @Mac,
    UserDate_Update = GETDATE()
WHERE SN = @SN;",
            p =>
            {
                p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
                p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)phone?.Trim() ?? DBNull.Value;
                p.Add("@Profit", SqlDbType.Decimal).Value = (object?)profitPercent ?? DBNull.Value;
                p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                p.Add("@SN", SqlDbType.Int).Value = sn;
            },
            cancellationToken);
    }

    public Task<int> DeleteAsync(int sn, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync(
            "DELETE FROM dbo.Account_SalesMan WHERE SN = @SN;",
            p => p.Add("@SN", SqlDbType.Int).Value = sn,
            cancellationToken);

    private async Task<int> ExecuteInsertWithFallbackAsync(
        string identitySql,
        string computedSnSql,
        string name,
        string? phone,
        decimal? profitPercent,
        AppSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _db.QueryAsync(identitySql,
                p => BuildInsertParameters(p, name, phone, profitPercent, session),
                cancellationToken);
            return Convert.ToInt32(result.Rows[0][0]);
        }
        catch (SqlException ex) when (ex.Number == 515)
        {
            var result = await _db.QueryAsync(computedSnSql,
                p => BuildInsertParameters(p, name, phone, profitPercent, session),
                cancellationToken);
            return Convert.ToInt32(result.Rows[0][0]);
        }
    }

    private static void BuildInsertParameters(
        SqlParameterCollection p,
        string name,
        string? phone,
        decimal? profitPercent,
        AppSession session)
    {
        p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
        p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)phone?.Trim() ?? DBNull.Value;
        p.Add("@Profit", SqlDbType.Decimal).Value = (object?)profitPercent ?? DBNull.Value;
        p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
        p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
        p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
    }

    private static void ValidateName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 300)
            throw new ArgumentException("اسم المندوب يجب أن يكون بين 1 و300 حرف.");
    }
}
