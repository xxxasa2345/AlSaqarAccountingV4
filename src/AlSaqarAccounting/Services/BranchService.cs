using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Account_Branch (الفروع). Columns come from the
/// forensic schema export only; every value is parameterized.
/// </summary>
public sealed class BranchService
{
    private readonly DbExecutor _db;

    public BranchService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(
            @"
SELECT ID, Name, AccountNo, Phone, Fax, Address, Note,
       UserBranch_Add, UserDate_Add, UserDate_Update
FROM dbo.Account_Branch
ORDER BY Name, ID;",
            cancellationToken: cancellationToken);

    public Task<int> CreateAsync(
        string name,
        string? phone,
        string? fax,
        string? address,
        string? note,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        const string sql = @"
INSERT INTO dbo.Account_Branch
    (Name, Phone, Fax, Address, Note,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
VALUES
    (@Name, @Phone, @Fax, @Address, @Note,
     @UserID, @BranchID, @Mac, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS int);";

        return ExecuteInsertWithFallbackAsync(sql, name, phone, fax, address, note, session, cancellationToken);
    }

    public Task<int> UpdateAsync(
        int id,
        string name,
        string? phone,
        string? fax,
        string? address,
        string? note,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        return _db.ExecuteAsync(
            @"
UPDATE dbo.Account_Branch
SET Name = @Name,
    Phone = @Phone,
    Fax = @Fax,
    Address = @Address,
    Note = @Note,
    UserID_Update = @UserID,
    UserBranch_Update = @BranchID,
    UserMacAddress_Update = @Mac,
    UserDate_Update = GETDATE()
WHERE ID = @ID;",
            p =>
            {
                p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
                p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)phone?.Trim() ?? DBNull.Value;
                p.Add("@Fax", SqlDbType.NVarChar, 100).Value = (object?)fax?.Trim() ?? DBNull.Value;
                p.Add("@Address", SqlDbType.NVarChar, 300).Value = (object?)address?.Trim() ?? DBNull.Value;
                p.Add("@Note", SqlDbType.NVarChar, 500).Value = (object?)note?.Trim() ?? DBNull.Value;
                p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                p.Add("@ID", SqlDbType.Int).Value = id;
            },
            cancellationToken);
    }

    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync(
            "DELETE FROM dbo.Account_Branch WHERE ID = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);

    private async Task<int> ExecuteInsertWithFallbackAsync(
        string identitySql,
        string name,
        string? phone,
        string? fax,
        string? address,
        string? note,
        AppSession session,
        CancellationToken cancellationToken)
    {
        const string computedIdSql = @"
INSERT INTO dbo.Account_Branch
    (ID, Name, Phone, Fax, Address, Note,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
SELECT
    (SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.Account_Branch),
    @Name, @Phone, @Fax, @Address, @Note,
    @UserID, @BranchID, @Mac, GETDATE();
SELECT (SELECT ISNULL(MAX(ID), 0) FROM dbo.Account_Branch);";

        try
        {
            var result = await _db.QueryAsync(identitySql,
                p => BuildInsertParameters(p, name, phone, fax, address, note, session),
                cancellationToken);
            return Convert.ToInt32(result.Rows[0][0]);
        }
        catch (SqlException ex) when (ex.Number == 515)
        {
            var result = await _db.QueryAsync(computedIdSql,
                p => BuildInsertParameters(p, name, phone, fax, address, note, session),
                cancellationToken);
            return Convert.ToInt32(result.Rows[0][0]);
        }
    }

    private static void BuildInsertParameters(
        SqlParameterCollection p,
        string name,
        string? phone,
        string? fax,
        string? address,
        string? note,
        AppSession session)
    {
        p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
        p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)phone?.Trim() ?? DBNull.Value;
        p.Add("@Fax", SqlDbType.NVarChar, 100).Value = (object?)fax?.Trim() ?? DBNull.Value;
        p.Add("@Address", SqlDbType.NVarChar, 300).Value = (object?)address?.Trim() ?? DBNull.Value;
        p.Add("@Note", SqlDbType.NVarChar, 500).Value = (object?)note?.Trim() ?? DBNull.Value;
        p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
        p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
        p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
    }

    private static void ValidateName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 300)
            throw new ArgumentException("اسم الفرع يجب أن يكون بين 1 و300 حرف.");
    }
}
