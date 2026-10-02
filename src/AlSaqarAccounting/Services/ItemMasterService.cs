using System.Data;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for the simple item master tables.
/// Table names are selected only from a fixed whitelist; values are parameterized.
/// </summary>
public sealed class ItemMasterService
{
    private readonly DbExecutor _db;

    public ItemMasterService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var table = NormalizeTable(tableName);
        return _db.QueryAsync(
            $"SELECT ID, Name, UserBranch_Add, UserDate_Add, UserDate_Update FROM dbo.[{table}] ORDER BY Name, ID;",
            cancellationToken: cancellationToken);
    }

    public async Task<int> CreateAsync(
        string tableName,
        string name,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        var table = NormalizeTable(tableName);
        ValidateName(name);

        const string sqlTemplate = @"
INSERT INTO dbo.[{0}]
    (Name, UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
VALUES
    (@Name, @UserID, @BranchID, @Mac, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var result = await _db.QueryAsync(
            string.Format(sqlTemplate, table),
            p =>
            {
                p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
                p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
            }, cancellationToken);

        if (result.Rows.Count == 0)
            throw new InvalidOperationException("لم تُرجع قاعدة البيانات رقم السجل الجديد.");

        return Convert.ToInt32(result.Rows[0][0]);
    }

    public Task<int> UpdateAsync(
        string tableName,
        int id,
        string name,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        var table = NormalizeTable(tableName);
        ValidateName(name);

        return _db.ExecuteAsync(
            $@"UPDATE dbo.[{table}]
SET Name=@Name,
    UserID_Update=@UserID,
    UserBranch_Update=@BranchID,
    UserMacAddress_Update=@Mac,
    UserDate_Update=GETDATE()
WHERE ID=@ID;",
            p =>
            {
                p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
                p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                p.Add("@ID", SqlDbType.Int).Value = id;
            }, cancellationToken);
    }

    public Task<int> DeleteAsync(string tableName, int id, CancellationToken cancellationToken = default)
    {
        var table = NormalizeTable(tableName);
        return _db.ExecuteAsync(
            $"DELETE FROM dbo.[{table}] WHERE ID=@ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);
    }

    private static string NormalizeTable(string tableName)
    {
        return tableName switch
        {
            "Item_Company" => "Item_Company",
            "Item_Class" => "Item_Class",
            "Item_Groups" => "Item_Groups",
            "Item_Country" => "Item_Country",
            "Item_Doctor" => "Item_Doctor",
            _ => throw new ArgumentOutOfRangeException(nameof(tableName), "مصدر شاشة البيانات الرئيسية غير مسموح به.")
        };
    }

    private static void ValidateName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 300)
            throw new ArgumentException("الاسم يجب أن يكون بين 1 و300 حرف.");
    }
}
