using System.Data;
using System.Data.SqlClient;

namespace AlSaqarAccounting.UI;

/// <summary>
/// Safe fallback until a matching original Form implementation is extracted.
/// It intentionally performs SELECT-only reads.
/// </summary>
public sealed class CatalogDataScreen : Form
{
    private readonly string _connectionString;
    private readonly string _screenName;
    private readonly string? _requestedTable;
    private readonly ScreenAccess _access;

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BackgroundColor = Color.White,
        RightToLeft = RightToLeft.Yes
    };

    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 34,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(8)
    };

    public CatalogDataScreen(
        string connectionString,
        string screenName,
        string? requestedTable,
        ScreenAccess access)
    {
        _connectionString = connectionString;
        _screenName = screenName;
        _requestedTable = requestedTable;
        _access = access;

        Text = "الصقر للمحاسبة — " + screenName;
        Width = 1180;
        Height = 760;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(12)
        };

        var title = new Label
        {
            Text = screenName,
            Dock = DockStyle.Top,
            Height = 38,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Tahoma", 16, FontStyle.Bold)
        };

        var permissions = new Label
        {
            Text = BuildPermissionText(),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.DimGray
        };

        header.Controls.Add(permissions);
        header.Controls.Add(title);

        var refresh = new Button
        {
            Text = "تحديث",
            Dock = DockStyle.Bottom,
            Height = 38,
            Enabled = _access.AllowEnter
        };
        refresh.Click += (_, _) => LoadData();

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(refresh);
        Controls.Add(header);

        Shown += (_, _) => LoadData();
    }

    private string BuildPermissionText()
    {
        var parts = new List<string> { "فتح" };
        if (_access.AllowSave) parts.Add("حفظ");
        if (_access.AllowEdit) parts.Add("تعديل");
        if (_access.AllowDelete) parts.Add("حذف");
        if (_access.AllowPrint) parts.Add("طباعة");
        if (_access.AllowExport) parts.Add("تصدير");
        if (_access.AllowBranch) parts.Add("تبديل الفرع");
        return "الصلاحيات: " + string.Join("  |  ", parts);
    }

    private void LoadData()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var table = FindTable(connection);
            if (string.IsNullOrWhiteSpace(table))
            {
                _grid.DataSource = null;
                _status.Text = "هذه شاشة أصلية من الفهرس، لكن لم يتم استخراج تنفيذ Form المقابل لها بعد.";
                return;
            }

            var safeTable = table.Replace("]", "]]");
            using var command = new SqlCommand(
                $"SELECT TOP (500) * FROM dbo.[{safeTable}]",
                connection);

            using var adapter = new SqlDataAdapter(command);
            var data = new DataTable();
            adapter.Fill(data);
            _grid.DataSource = data;
            _status.Text = $"الجدول: {table} — سجلات معروضة: {data.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            _grid.DataSource = null;
            _status.Text = ex.GetBaseException().Message;
        }
    }

    private string? FindTable(SqlConnection connection)
    {
        if (!string.IsNullOrWhiteSpace(_requestedTable) && TableExists(connection, _requestedTable))
            return _requestedTable;

        if (TableExists(connection, _screenName))
            return _screenName;

        var normalized = _screenName
            .Replace("Frm", "")
            .Replace(" ", "");

        if (TableExists(connection, normalized))
            return normalized;

        return null;
    }

    private static bool TableExists(SqlConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo'
              AND TABLE_NAME = @Name
              AND TABLE_TYPE = 'BASE TABLE';
            """;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 128).Value = name;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }
}
