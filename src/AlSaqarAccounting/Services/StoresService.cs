using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Account_Stores (المخازن). Columns come from the
/// forensic schema export only; every value is parameterized.
/// </summary>
public sealed class StoresService
{
    private readonly DbExecutor _db;

    public StoresService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(
            @"
SELECT ID, Store_Name, BranchID, Phone, Fax, Address,
       UserDate_Add, UserDate_Update
FROM dbo.Account_Stores
ORDER BY Store_Name, ID;",
            cancellationToken: cancellationToken);

    public Task<int> CreateAsync(
        string name,
        string? phone,
        string? address,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        const string identitySql = @"
INSERT INTO dbo.Account_Stores
    (Store_Name, Phone, Address, BranchID,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
VALUES
    (@Name, @Phone, @Address, @BranchID,
     @UserID, @BranchID, @Mac, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS int);";

        const string computedIdSql = @"
INSERT INTO dbo.Account_Stores
    (ID, Store_Name, Phone, Address, BranchID,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
SELECT
    (SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.Account_Stores),
    @Name, @Phone, @Address, @BranchID,
    @UserID, @BranchID, @Mac, GETDATE();
SELECT (SELECT ISNULL(MAX(ID), 0) FROM dbo.Account_Stores);";

        return ExecuteInsertWithFallbackAsync(identitySql, computedIdSql, name, phone, address, session, cancellationToken);
    }

    public Task<int> UpdateAsync(
        int id,
        string name,
        string? phone,
        string? address,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        return _db.ExecuteAsync(
            @"
UPDATE dbo.Account_Stores
SET Store_Name = @Name,
    Phone = @Phone,
    Address = @Address,
    UserID_Update = @UserID,
    UserBranch_Update = @BranchID,
    UserMacAddress_Update = @Mac,
    UserDate_Update = GETDATE()
WHERE ID = @ID;",
            p =>
            {
                p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
                p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)phone?.Trim() ?? DBNull.Value;
                p.Add("@Address", SqlDbType.NVarChar, 300).Value = (object?)address?.Trim() ?? DBNull.Value;
                p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                p.Add("@ID", SqlDbType.Int).Value = id;
            },
            cancellationToken);
    }

    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync(
            "DELETE FROM dbo.Account_Stores WHERE ID = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);

    private async Task<int> ExecuteInsertWithFallbackAsync(
        string identitySql,
        string computedIdSql,
        string name,
        string? phone,
        string? address,
        AppSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _db.QueryAsync(identitySql,
                p => BuildInsertParameters(p, name, phone, address, session),
                cancellationToken);
            return Convert.ToInt32(result.Rows[0][0]);
        }
        catch (SqlException ex) when (ex.Number == 515)
        {
            var result = await _db.QueryAsync(computedIdSql,
                p => BuildInsertParameters(p, name, phone, address, session),
                cancellationToken);
            return Convert.ToInt32(result.Rows[0][0]);
        }
    }

    private static void BuildInsertParameters(
        SqlParameterCollection p,
        string name,
        string? phone,
        string? address,
        AppSession session)
    {
        p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
        p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)phone?.Trim() ?? DBNull.Value;
        p.Add("@Address", SqlDbType.NVarChar, 300).Value = (object?)address?.Trim() ?? DBNull.Value;
        p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
        p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
        p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
    }

    private static void ValidateName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 300)
            throw new ArgumentException("اسم المخزن يجب أن يكون بين 1 و300 حرف.");
    }
}
