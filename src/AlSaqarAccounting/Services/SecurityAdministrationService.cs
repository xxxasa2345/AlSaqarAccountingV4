using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Administrative security/catalog operations over the original GTSdb2026
/// tables User_Screens, User_Permission and User_Login.
/// </summary>
public sealed class SecurityAdministrationService
{
    private readonly DbExecutor _db;

    public SecurityAdministrationService(DbExecutor db) => _db = db;

    public Task<DataTable> GetGroupsAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(@"
SELECT ID, Name
FROM dbo.User_Groups
ORDER BY ID;", cancellationToken: cancellationToken);

    public Task<DataTable> GetScreensAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(@"
SELECT ID, Screen_Name, ScreenTypeID, ScreenNum, ScreenTypeName, ISShow
FROM dbo.User_Screens
ORDER BY ISNULL(ScreenTypeID, 0),
         ISNULL(ScreenNum, 2147483647),
         ID;", cancellationToken: cancellationToken);

    public async Task SaveScreenAsync(
        int? id,
        string name,
        int? screenTypeId,
        int? screenNum,
        string? screenTypeName,
        bool isShow,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم الشاشة مطلوب.", nameof(name));

        if (id.HasValue)
        {
            await _db.ExecuteAsync(@"
UPDATE dbo.User_Screens
SET Screen_Name=@Name,
    ScreenTypeID=@TypeID,
    ScreenNum=@ScreenNum,
    ScreenTypeName=@TypeName,
    ISShow=@IsShow
WHERE ID=@ID;",
                p =>
                {
                    p.Add("@ID", SqlDbType.Int).Value = id.Value;
                    AddScreenValues(p, name, screenTypeId, screenNum, screenTypeName, isShow);
                },
                cancellationToken);
            return;
        }

        await _db.ExecuteAsync(@"
INSERT INTO dbo.User_Screens
(Screen_Name, ScreenTypeID, ScreenNum, ScreenTypeName, ISShow)
VALUES
(@Name, @TypeID, @ScreenNum, @TypeName, @IsShow);",
            p => AddScreenValues(p, name, screenTypeId, screenNum, screenTypeName, isShow),
            cancellationToken);
    }

    public async Task DeleteScreenAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var usage = await _db.QueryAsync(
            "SELECT COUNT(1) AS UsageCount FROM dbo.User_Permission WHERE ScreenID=@ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);

        if (usage.Rows.Count > 0 &&
            Convert.ToInt32(usage.Rows[0]["UsageCount"]) > 0)
            throw new InvalidOperationException(
                "لا يمكن حذف الشاشة لأنها مرتبطة بصلاحيات مستخدمين. احذف الصلاحيات المرتبطة أولاً.");

        await _db.ExecuteAsync(
            "DELETE FROM dbo.User_Screens WHERE ID=@ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);
    }

    public Task<DataTable> GetPermissionsAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(@"
SELECT
    p.ID,
    p.GroupID,
    g.Name AS GroupName,
    p.ScreenID,
    s.Screen_Name AS ScreenName,
    p.Allow_Branch,
    p.Allow_Enter,
    p.Allow_Save,
    p.Allow_Edit,
    p.Allow_Delete,
    p.Allow_Print,
    p.Allow_Export
FROM dbo.User_Permission AS p
LEFT JOIN dbo.User_Groups AS g ON g.ID = p.GroupID
LEFT JOIN dbo.User_Screens AS s ON s.ID = p.ScreenID
ORDER BY p.GroupID,
         ISNULL(s.ScreenTypeID, 0),
         ISNULL(s.ScreenNum, 2147483647),
         p.ScreenID,
         p.ID;", cancellationToken: cancellationToken);

    public async Task SavePermissionAsync(
        int? id,
        int groupId,
        int screenId,
        bool allowBranch,
        bool allowEnter,
        bool allowSave,
        bool allowEdit,
        bool allowDelete,
        bool allowPrint,
        bool allowExport,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (groupId <= 0)
            throw new ArgumentException("مجموعة المستخدمين مطلوبة.", nameof(groupId));
        if (screenId <= 0)
            throw new ArgumentException("الشاشة مطلوبة.", nameof(screenId));

        if (id.HasValue)
        {
            await _db.ExecuteAsync(@"
UPDATE dbo.User_Permission
SET GroupID=@GroupID,
    ScreenID=@ScreenID,
    Allow_Branch=@AllowBranch,
    Allow_Enter=@AllowEnter,
    Allow_Save=@AllowSave,
    Allow_Edit=@AllowEdit,
    Allow_Delete=@AllowDelete,
    Allow_Print=@AllowPrint,
    Allow_Export=@AllowExport,
    UserID_Update=@Actor,
    UserBranch_Update=@ActorBranch,
    UserMacAddress_Update=@Machine,
    UserDate_Update=GETDATE()
WHERE ID=@ID;",
                p =>
                {
                    p.Add("@ID", SqlDbType.Int).Value = id.Value;
                    AddPermissionValues(
                        p, groupId, screenId, allowBranch, allowEnter, allowSave,
                        allowEdit, allowDelete, allowPrint, allowExport);
                    p.Add("@Actor", SqlDbType.Int).Value = session.UserId;
                    p.Add("@ActorBranch", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                    p.Add("@Machine", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                },
                cancellationToken);
            return;
        }

        var existing = await _db.QueryAsync(@"
SELECT TOP (1) ID
FROM dbo.User_Permission
WHERE GroupID=@GroupID AND ScreenID=@ScreenID
ORDER BY ID;",
            p =>
            {
                p.Add("@GroupID", SqlDbType.Int).Value = groupId;
                p.Add("@ScreenID", SqlDbType.Int).Value = screenId;
            },
            cancellationToken);

        if (existing.Rows.Count > 0)
        {
            var existingId = Convert.ToInt32(existing.Rows[0]["ID"]);
            await SavePermissionAsync(
                existingId, groupId, screenId, allowBranch, allowEnter, allowSave,
                allowEdit, allowDelete, allowPrint, allowExport, session, cancellationToken);
            return;
        }

        await _db.ExecuteAsync(@"
INSERT INTO dbo.User_Permission
(GroupID, ScreenID,
 Allow_Branch, Allow_Enter, Allow_Save, Allow_Edit, Allow_Delete,
 Allow_Print, Allow_Export,
 UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
VALUES
(@GroupID, @ScreenID,
 @AllowBranch, @AllowEnter, @AllowSave, @AllowEdit, @AllowDelete,
 @AllowPrint, @AllowExport,
 @Actor, @ActorBranch, @Machine, GETDATE());",
            p =>
            {
                AddPermissionValues(
                    p, groupId, screenId, allowBranch, allowEnter, allowSave,
                    allowEdit, allowDelete, allowPrint, allowExport);
                p.Add("@Actor", SqlDbType.Int).Value = session.UserId;
                p.Add("@ActorBranch", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Machine", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
            },
            cancellationToken);
    }

    public Task DeletePermissionAsync(
        int id,
        CancellationToken cancellationToken = default)
        => _db.ExecuteAsync(
            "DELETE FROM dbo.User_Permission WHERE ID=@ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);

    public async Task ChangePasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("معرف المستخدم غير صالح.", nameof(userId));
        if (string.IsNullOrEmpty(newPassword))
            throw new ArgumentException("كلمة المرور الجديدة مطلوبة.", nameof(newPassword));
        if (newPassword.Length < 4)
            throw new ArgumentException("كلمة المرور الجديدة يجب ألا تقل عن 4 أحرف.", nameof(newPassword));

        var current = await _db.QuerySingleAsync<UserPasswordRow>(
            "SELECT TOP (1) PassWord FROM dbo.User_Login WHERE ID=@ID AND ISNULL(IsActive,1)=1;",
            p => p.Add("@ID", SqlDbType.Int).Value = userId,
            cancellationToken);

        if (current is null)
            throw new InvalidOperationException("المستخدم الحالي غير موجود أو غير نشط.");

        if (!string.Equals(current.PassWord ?? string.Empty, currentPassword ?? string.Empty, StringComparison.Ordinal))
            throw new InvalidOperationException("كلمة المرور الحالية غير صحيحة.");

        await _db.ExecuteAsync(@"
UPDATE dbo.User_Login
SET PassWord=@NewPassword,
    UserID_Update=@Actor,
    UserBranch_Update=@ActorBranch,
    UserMacAddress_Update=@Machine,
    UserDate_Update=GETDATE()
WHERE ID=@ID;",
            p =>
            {
                p.Add("@ID", SqlDbType.Int).Value = userId;
                p.Add("@NewPassword", SqlDbType.NVarChar, -1).Value = newPassword;
                p.Add("@Actor", SqlDbType.Int).Value = session.UserId;
                p.Add("@ActorBranch", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Machine", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
            },
            cancellationToken);
    }

    private static void AddScreenValues(
        SqlParameterCollection p,
        string name,
        int? typeId,
        int? screenNum,
        string? typeName,
        bool isShow)
    {
        p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
        p.Add("@TypeID", SqlDbType.Int).Value = (object?)typeId ?? DBNull.Value;
        p.Add("@ScreenNum", SqlDbType.Int).Value = (object?)screenNum ?? DBNull.Value;
        p.Add("@TypeName", SqlDbType.NVarChar, 300).Value =
            (object?) (string.IsNullOrWhiteSpace(typeName) ? null : typeName.Trim()) ?? DBNull.Value;
        p.Add("@IsShow", SqlDbType.Bit).Value = isShow;
    }

    private static void AddPermissionValues(
        SqlParameterCollection p,
        int groupId,
        int screenId,
        bool allowBranch,
        bool allowEnter,
        bool allowSave,
        bool allowEdit,
        bool allowDelete,
        bool allowPrint,
        bool allowExport)
    {
        p.Add("@GroupID", SqlDbType.Int).Value = groupId;
        p.Add("@ScreenID", SqlDbType.Int).Value = screenId;
        p.Add("@AllowBranch", SqlDbType.Bit).Value = allowBranch;
        p.Add("@AllowEnter", SqlDbType.Bit).Value = allowEnter;
        p.Add("@AllowSave", SqlDbType.Bit).Value = allowSave;
        p.Add("@AllowEdit", SqlDbType.Bit).Value = allowEdit;
        p.Add("@AllowDelete", SqlDbType.Bit).Value = allowDelete;
        p.Add("@AllowPrint", SqlDbType.Bit).Value = allowPrint;
        p.Add("@AllowExport", SqlDbType.Bit).Value = allowExport;
    }

    private sealed class UserPasswordRow
    {
        public string? PassWord { get; set; }
    }
}
