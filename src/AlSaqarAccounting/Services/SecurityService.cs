using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Reads the original security model from GTSdb2026.
/// Read-only: no permission rows are inserted/updated/deleted here.
/// </summary>
public sealed class SecurityService
{
    private readonly SqlConnectionFactory _factory;

    public SecurityService(SqlConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<ScreenAccess>> GetAccessibleScreensAsync(
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (session.GroupId is null)
            return Array.Empty<ScreenAccess>();

        const string sql = """
            SELECT
                s.ID,
                s.Screen_Name,
                s.ScreenTypeID,
                s.ScreenNum,
                s.ScreenTypeName,
                s.ISShow,
                p.Allow_Branch,
                p.Allow_Enter,
                p.Allow_Save,
                p.Allow_Edit,
                p.Allow_Delete,
                p.Allow_Print,
                p.Allow_Export
            FROM dbo.User_Screens AS s
            INNER JOIN dbo.User_Permission AS p
                ON p.ScreenID = s.ID
               AND p.GroupID = @GroupID
            WHERE ISNULL(s.ISShow, 1) = 1
              AND ISNULL(p.Allow_Enter, 0) = 1
            ORDER BY
                ISNULL(s.ScreenTypeID, 0),
                ISNULL(s.ScreenNum, 2147483647),
                s.ID;
            """;

        using var cn = _factory.Create();
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@GroupID", SqlDbType.Int).Value = session.GroupId.Value;
        await cn.OpenAsync(cancellationToken);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var result = new List<ScreenAccess>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ScreenAccess
            {
                Id = reader.GetInt32(0),
                ScreenName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ScreenTypeId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                ScreenNum = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                ScreenTypeName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                IsShow = !reader.IsDBNull(5) && reader.GetBoolean(5),
                AllowBranch = !reader.IsDBNull(6) && reader.GetBoolean(6),
                AllowEnter = !reader.IsDBNull(7) && reader.GetBoolean(7),
                AllowSave = !reader.IsDBNull(8) && reader.GetBoolean(8),
                AllowEdit = !reader.IsDBNull(9) && reader.GetBoolean(9),
                AllowDelete = !reader.IsDBNull(10) && reader.GetBoolean(10),
                AllowPrint = !reader.IsDBNull(11) && reader.GetBoolean(11),
                AllowExport = !reader.IsDBNull(12) && reader.GetBoolean(12)
            });
        }

        return result;
    }

    public async Task<string?> GetGroupNameAsync(
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (session.GroupId is null)
            return null;

        const string sql = """
            SELECT TOP (1) Name
            FROM dbo.User_Groups
            WHERE ID = @GroupID;
            """;

        using var cn = _factory.Create();
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@GroupID", SqlDbType.Int).Value = session.GroupId.Value;
        await cn.OpenAsync(cancellationToken);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? null : value.ToString();
    }
}
