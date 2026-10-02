using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

public enum PermissionAction
{
    Enter,
    Save,
    Edit,
    Delete,
    Print,
    Export,
    Branch
}

/// <summary>
/// Central authorization guard. Permissions are re-read from the database
/// using the logged-in user's current group and the target screen.
/// This prevents write operations from depending only on WinForms controls.
/// </summary>
public sealed class AuthorizationService
{
    private readonly DbExecutor _db;

    public AuthorizationService(DbExecutor db) => _db = db;

    public async Task RequireAsync(
        AppSession session,
        int screenId,
        PermissionAction action,
        CancellationToken cancellationToken = default)
    {
        if (session.UserId <= 0)
            throw new UnauthorizedAccessException("جلسة المستخدم غير صالحة.");

        if (!session.GroupId.HasValue || session.GroupId.Value <= 0)
            throw new UnauthorizedAccessException("المستخدم غير مرتبط بمجموعة صلاحيات.");

        if (screenId <= 0)
            throw new ArgumentOutOfRangeException(nameof(screenId), "معرف الشاشة غير صالح.");

        const string sql = @"
SELECT TOP (1)
    ISNULL(p.Allow_Enter, 0),
    ISNULL(p.Allow_Save, 0),
    ISNULL(p.Allow_Edit, 0),
    ISNULL(p.Allow_Delete, 0),
    ISNULL(p.Allow_Print, 0),
    ISNULL(p.Allow_Export, 0),
    ISNULL(p.Allow_Branch, 0)
FROM dbo.User_Login AS u
INNER JOIN dbo.User_Permission AS p
    ON p.GroupID = u.GroupID
INNER JOIN dbo.User_Screens AS s
    ON s.ID = p.ScreenID
WHERE u.ID = @UserId
  AND ISNULL(u.IsActive, 0) = 1
  AND u.GroupID = @GroupId
  AND p.ScreenID = @ScreenId
  AND ISNULL(s.ISShow, 1) = 1;";

        var table = await _db.QueryAsync(
            sql,
            p =>
            {
                p.Add("@UserId", SqlDbType.Int).Value = session.UserId;
                p.Add("@GroupId", SqlDbType.Int).Value = session.GroupId.Value;
                p.Add("@ScreenId", SqlDbType.Int).Value = screenId;
            },
            cancellationToken).ConfigureAwait(false);

        if (table.Rows.Count == 0)
            throw new UnauthorizedAccessException("لا توجد صلاحية لهذا الإجراء على الشاشة الحالية.");

        var row = table.Rows[0];
        var allowed = action switch
        {
            PermissionAction.Enter => ToBool(row[0]),
            PermissionAction.Save => ToBool(row[1]),
            PermissionAction.Edit => ToBool(row[2]),
            PermissionAction.Delete => ToBool(row[3]),
            PermissionAction.Print => ToBool(row[4]),
            PermissionAction.Export => ToBool(row[5]),
            PermissionAction.Branch => ToBool(row[6]),
            _ => false
        };

        if (!allowed)
            throw new UnauthorizedAccessException($"لا تملك صلاحية {GetActionName(action)} لهذه الشاشة.");
    }

    private static bool ToBool(object value)
        => value != DBNull.Value && Convert.ToBoolean(value);

    private static string GetActionName(PermissionAction action) => action switch
    {
        PermissionAction.Enter => "فتح",
        PermissionAction.Save => "الحفظ",
        PermissionAction.Edit => "التعديل",
        PermissionAction.Delete => "الحذف",
        PermissionAction.Print => "الطباعة",
        PermissionAction.Export => "التصدير",
        PermissionAction.Branch => "تبديل الفرع",
        _ => "هذا الإجراء"
    };
}
