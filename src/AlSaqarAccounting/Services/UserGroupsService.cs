using System.Data;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real CRUD service for dbo.User_Groups, the security-group entity used by
/// the original GTSErpSystem security module (FrmGroups).
/// </summary>
public sealed class UserGroupsService
{
    private readonly DbExecutor _db;

    public UserGroupsService(DbExecutor db) => _db = db;

    public Task<DataTable> GetGroupsAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(@"
SELECT ID, Name, BranchID,
       SellPrice2, SellPrice3, LastPrice, AvergPrice, ProvePrice,
       SelectSellPrice, SelectQuantityPrice,
       UserID_Add, UserBranch_Add, UserDate_Add,
       UserID_Update, UserBranch_Update, UserDate_Update
FROM dbo.User_Groups
ORDER BY ID;", cancellationToken: cancellationToken);

    public async Task SaveAsync(
        int? id,
        string name,
        int? branchId,
        bool sellPrice2,
        bool sellPrice3,
        bool lastPrice,
        bool avergPrice,
        bool provePrice,
        int selectSellPrice,
        bool selectQuantityPrice,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم المجموعة مطلوب.", nameof(name));

        if (id.HasValue)
        {
            await _db.ExecuteAsync(@"
UPDATE dbo.User_Groups
SET Name=@Name,
    BranchID=@BranchID,
    SellPrice2=@SellPrice2,
    SellPrice3=@SellPrice3,
    LastPrice=@LastPrice,
    AvergPrice=@AvergPrice,
    ProvePrice=@ProvePrice,
    SelectSellPrice=@SelectSellPrice,
    SelectQuantityPrice=@SelectQuantityPrice,
    UserID_Update=@Actor,
    UserBranch_Update=@ActorBranch,
    UserDate_Update=GETDATE()
WHERE ID=@ID;",
                p =>
                {
                    p.AddWithValue("@ID", id.Value);
                    AddValues(p, name, branchId, sellPrice2, sellPrice3, lastPrice,
                        avergPrice, provePrice, selectSellPrice, selectQuantityPrice);
                    p.AddWithValue("@Actor", session.UserId);
                    p.AddWithValue("@ActorBranch", (object?)session.BranchId ?? DBNull.Value);
                },
                cancellationToken);
            return;
        }

        await _db.ExecuteAsync(@"
INSERT INTO dbo.User_Groups
(Name, BranchID, SellPrice2, SellPrice3, LastPrice, AvergPrice, ProvePrice,
 SelectSellPrice, SelectQuantityPrice, UserID_Add, UserBranch_Add,
 UserDate_Add, UserMacAddress_Add)
VALUES
(@Name, @BranchID, @SellPrice2, @SellPrice3, @LastPrice, @AvergPrice, @ProvePrice,
 @SelectSellPrice, @SelectQuantityPrice, @Actor, @ActorBranch,
 GETDATE(), @Machine);",
            p =>
            {
                AddValues(p, name, branchId, sellPrice2, sellPrice3, lastPrice,
                    avergPrice, provePrice, selectSellPrice, selectQuantityPrice);
                p.AddWithValue("@Actor", session.UserId);
                p.AddWithValue("@ActorBranch", (object?)session.BranchId ?? DBNull.Value);
                p.AddWithValue("@Machine", Environment.MachineName);
            },
            cancellationToken);
    }

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var usage = await _db.QueryAsync(
            "SELECT COUNT(1) AS UsageCount FROM dbo.User_Login WHERE GroupID=@ID;",
            p => p.AddWithValue("@ID", id),
            cancellationToken);

        if (usage.Rows.Count > 0 && Convert.ToInt32(usage.Rows[0]["UsageCount"]) > 0)
            throw new InvalidOperationException("لا يمكن حذف المجموعة لأنها مرتبطة بمستخدمين. عدّل المستخدمين أولاً.");

        await _db.ExecuteAsync(
            "DELETE FROM dbo.User_Groups WHERE ID=@ID;",
            p => p.AddWithValue("@ID", id),
            cancellationToken);
    }

    private static void AddValues(
        System.Data.SqlClient.SqlParameterCollection p,
        string name,
        int? branchId,
        bool sellPrice2,
        bool sellPrice3,
        bool lastPrice,
        bool avergPrice,
        bool provePrice,
        int selectSellPrice,
        bool selectQuantityPrice)
    {
        p.AddWithValue("@Name", name.Trim());
        p.AddWithValue("@BranchID", (object?)branchId ?? DBNull.Value);
        p.AddWithValue("@SellPrice2", sellPrice2);
        p.AddWithValue("@SellPrice3", sellPrice3);
        p.AddWithValue("@LastPrice", lastPrice);
        p.AddWithValue("@AvergPrice", avergPrice);
        p.AddWithValue("@ProvePrice", provePrice);
        p.AddWithValue("@SelectSellPrice", selectSellPrice);
        p.AddWithValue("@SelectQuantityPrice", selectQuantityPrice);
    }
}
