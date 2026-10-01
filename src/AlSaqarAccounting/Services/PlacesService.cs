using System.Data;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real CRUD service for dbo.Account_Place, the entity behind the original
/// GTSErpSystem.Frms.Cust_Sup.FrmPlace screen.
/// </summary>
public sealed class PlacesService
{
    private readonly DbExecutor _db;

    public PlacesService(DbExecutor db) => _db = db;

    public Task<DataTable> GetPlacesAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(@"
SELECT ID, Name, UserID_Add, UserBranch_Add, UserDate_Add,
       UserID_Update, UserBranch_Update, UserDate_Update
FROM dbo.Account_Place
ORDER BY ID;", cancellationToken: cancellationToken);

    public Task<int> SaveAsync(
        int? id,
        string name,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم المنطقة مطلوب.", nameof(name));

        if (id.HasValue)
        {
            return _db.ExecuteAsync(@"
UPDATE dbo.Account_Place
SET Name=@Name,
    UserID_Update=@Actor,
    UserBranch_Update=@ActorBranch,
    UserDate_Update=GETDATE()
WHERE ID=@ID;",
                p =>
                {
                    p.AddWithValue("@ID", id.Value);
                    p.AddWithValue("@Name", name.Trim());
                    p.AddWithValue("@Actor", session.UserId);
                    p.AddWithValue("@ActorBranch", (object?)session.BranchId ?? DBNull.Value);
                },
                cancellationToken);
        }

        return _db.ExecuteAsync(@"
INSERT INTO dbo.Account_Place
(Name, UserID_Add, UserBranch_Add, UserDate_Add, UserMacAddress_Add)
VALUES (@Name, @Actor, @ActorBranch, GETDATE(), @Machine);",
            p =>
            {
                p.AddWithValue("@Name", name.Trim());
                p.AddWithValue("@Actor", session.UserId);
                p.AddWithValue("@ActorBranch", (object?)session.BranchId ?? DBNull.Value);
                p.AddWithValue("@Machine", Environment.MachineName);
            },
            cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var usage = await _db.QueryAsync(@"
SELECT COUNT(1) AS UsageCount
FROM dbo.Account_CustSup
WHERE PlaceID=@ID;",
            p => p.AddWithValue("@ID", id),
            cancellationToken);

        if (usage.Rows.Count > 0 && Convert.ToInt32(usage.Rows[0]["UsageCount"]) > 0)
            throw new InvalidOperationException("لا يمكن حذف المنطقة لأنها مرتبطة بعملاء أو موردين.");

        await _db.ExecuteAsync(
            "DELETE FROM dbo.Account_Place WHERE ID=@ID;",
            p => p.AddWithValue("@ID", id),
            cancellationToken);
    }
}
