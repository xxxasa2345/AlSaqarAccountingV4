using System.Data;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

public sealed class UserManagementService
{
    private readonly DbExecutor _db;
    public UserManagementService(DbExecutor db) => _db = db;

    public Task<DataTable> GetUsersAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(@"
SELECT ID, Name, BranchID, GroupID, IsActive, Phone, IsOpenDay, MoneyOpenDay,
       Note, DateOpen, DateClose, ModeInvoice
FROM dbo.User_Login ORDER BY ID;", cancellationToken: cancellationToken);

    public async Task<int> SaveAsync(int? id, string name, string password, int? branchId,
        int? groupId, bool isActive, string? phone, string? note,
        int actorUserId, int? actorBranchId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("اسم المستخدم مطلوب.", nameof(name));
        if (id.HasValue)
        {
            const string sql = @"
UPDATE dbo.User_Login SET Name=@Name,
 PassWord=CASE WHEN @Password='' THEN PassWord ELSE @Password END,
 BranchID=@BranchID, GroupID=@GroupID, IsActive=@IsActive, Phone=@Phone, Note=@Note,
 UserID_Update=@Actor, UserBranch_Update=@ActorBranch, UserDate_Update=GETDATE()
WHERE ID=@ID;";
            return await _db.ExecuteAsync(sql, p =>
            {
                p.AddWithValue("@ID", id.Value); p.AddWithValue("@Name", name.Trim());
                p.AddWithValue("@Password", password ?? string.Empty);
                p.AddWithValue("@BranchID", (object?)branchId ?? DBNull.Value);
                p.AddWithValue("@GroupID", (object?)groupId ?? DBNull.Value);
                p.AddWithValue("@IsActive", isActive);
                p.AddWithValue("@Phone", (object?)phone?.Trim() ?? DBNull.Value);
                p.AddWithValue("@Note", (object?)note?.Trim() ?? DBNull.Value);
                p.AddWithValue("@Actor", actorUserId);
                p.AddWithValue("@ActorBranch", (object?)actorBranchId ?? DBNull.Value);
            }, cancellationToken);
        }

        const string insert = @"
INSERT INTO dbo.User_Login
(Name, PassWord, BranchID, GroupID, IsActive, UserID_Add, UserBranch_Add, UserDate_Add, Phone, Note)
VALUES (@Name,@Password,@BranchID,@GroupID,@IsActive,@Actor,@ActorBranch,GETDATE(),@Phone,@Note);";
        return await _db.ExecuteAsync(insert, p =>
        {
            p.AddWithValue("@Name", name.Trim()); p.AddWithValue("@Password", password ?? string.Empty);
            p.AddWithValue("@BranchID", (object?)branchId ?? DBNull.Value);
            p.AddWithValue("@GroupID", (object?)groupId ?? DBNull.Value);
            p.AddWithValue("@IsActive", isActive); p.AddWithValue("@Actor", actorUserId);
            p.AddWithValue("@ActorBranch", (object?)actorBranchId ?? DBNull.Value);
            p.AddWithValue("@Phone", (object?)phone?.Trim() ?? DBNull.Value);
            p.AddWithValue("@Note", (object?)note?.Trim() ?? DBNull.Value);
        }, cancellationToken);
    }

    public Task<int> DeactivateAsync(int id, int actorUserId, int? actorBranchId, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync(@"UPDATE dbo.User_Login SET IsActive=0, UserID_Update=@Actor,
UserBranch_Update=@ActorBranch, UserDate_Update=GETDATE() WHERE ID=@ID;",
            p => { p.AddWithValue("@ID", id); p.AddWithValue("@Actor", actorUserId); p.AddWithValue("@ActorBranch", (object?)actorBranchId ?? DBNull.Value); }, cancellationToken);
}
