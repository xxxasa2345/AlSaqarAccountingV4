using AlSaqarAccounting.Core;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Common chrome for the concrete operational screens. Every concrete screen
/// is a distinct real Form with its own service; this base only removes
/// duplicated UI wiring. It contains no data access. Concrete screens add
/// their write buttons (new / delete) by overriding AddToolbarButtons.
/// </summary>
public abstract class BrowseScreenBase : Form
{
    protected readonly AppSession Session;
    protected readonly ScreenAccess Access;

    protected readonly TextBox Search = new()
    {
        Dock = DockStyle.Fill,
        RightToLeft = RightToLeft.Yes
    };

    protected readonly DataGridView Grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BackgroundColor = Color.White,
        RightToLeft = RightToLeft.Yes,
        RowHeadersVisible = false,
        AllowUserToResizeRows = false
    };

    protected readonly Label Status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(8),
        BorderStyle = BorderStyle.FixedSingle
    };

    private DataTable? _data;

    protected BrowseScreenBase(AppSession session, ScreenAccess access)
    {
        Session = session;
        Access = access;

        Text = "الصقر للمحاسبة — " + ScreenTitle;
        Width = 1240;
        Height = 780;
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildLayout();
        WireEvents();
    }

    protected abstract string ScreenTitle { get; }

    protected abstract Task<DataTable> LoadDataAsync();

    /// <summary>Hook for concrete screens to add their operational buttons
    /// (فاتورة جديدة، حذف، سند جديد ...) to the shared toolbar.</summary>
    protected virtual void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
    }

    /// <summary>Currently selected grid row, or null.</summary>
    protected DataRowView? CurrentRow => Grid.CurrentRow?.DataBoundItem as DataRowView;

    /// <summary>Returns the value of the first matching column (by name,
    /// case-insensitive) of the given row, or null when none exists.</summary>
    protected static object? RowValue(DataRowView? row, params string[] candidates)
    {
        if (row is null || candidates.Length == 0)
            return null;

        foreach (DataColumn column in row.Row.Table.Columns)
        {
            foreach (var candidate in candidates)
            {
                if (string.Equals(column.ColumnName, candidate, StringComparison.OrdinalIgnoreCase))
                    return row.Row[column];
            }
        }

        return null;
    }

    /// <summary>Reads an integer id from the current row trying several
    /// common column names.</summary>
    protected static bool TryRowId(DataRowView? row, out int id)
    {
        id = 0;
        var value = RowValue(row, "ID", "id", "SN", "Sn", "PurBranchID", "Account_No", "DocCode");
        if (value is null or DBNull)
            return false;

        try
        {
            id = Convert.ToInt32(value);
            return id > 0;
        }
        catch
        {
            return false;
        }
    }

    private void BuildLayout()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 122,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 247, 250)
        };

        var title = new Label
        {
            Text = ScreenTitle,
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };

        var info = new Label
        {
            Text = $"المستخدم: {Session.UserName} | الفرع: {Session.BranchId?.ToString() ?? "-"}",
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };

        var permissions = new Label
        {
            Text = BuildPermissionText(),
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };

        var searchRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 32, ColumnCount = 2 };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        var refresh = new Button { Text = "تحديث", Dock = DockStyle.Fill, Enabled = Access.AllowEnter };
        refresh.Click += async (_, _) => await ReloadAsync();
        searchRow.Controls.Add(Search, 0, 0);
        searchRow.Controls.Add(refresh, 1, 0);

        header.Controls.Add(searchRow);
        header.Controls.Add(permissions);
        header.Controls.Add(info);
        header.Controls.Add(title);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
            WrapContents = false
        };
        AddButton(toolbar, "تفاصيل", true, () => ScreenToolbox.ShowRecordDetails(this, ScreenTitle, Grid));
        AddButton(toolbar, "تصدير CSV", Access.AllowExport, () => Export());
        AddButton(toolbar, "طباعة", Access.AllowPrint, () => Print());
        AddToolbarButtons(toolbar);

        Controls.Add(Grid);
        Controls.Add(Status);
        Controls.Add(toolbar);
        Controls.Add(header);
    }

    private string BuildPermissionText()
    {
        var parts = new List<string>();
        if (Access.AllowEnter) parts.Add("فتح");
        if (Access.AllowSave) parts.Add("إضافة");
        if (Access.AllowEdit) parts.Add("تعديل");
        if (Access.AllowDelete) parts.Add("حذف");
        if (Access.AllowPrint) parts.Add("طباعة");
        if (Access.AllowExport) parts.Add("تصدير");
        return parts.Count == 0
            ? "الصلاحيات: لا توجد عمليات متاحة"
            : "الصلاحيات: " + string.Join("  |  ", parts);
    }

    private void WireEvents()
    {
        Search.TextChanged += (_, _) => ApplySearch();
        Grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0) ScreenToolbox.ShowRecordDetails(this, ScreenTitle, Grid);
        };
        Shown += async (_, _) => await ReloadAsync();
    }

    private static void AddButton(FlowLayoutPanel toolbar, string text, bool enabled, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 105,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat
        };
        button.Click += (_, _) => action();
        toolbar.Controls.Add(button);
    }

    /// <summary>Reloads the screen data; called on show, on refresh and after
    /// every write operation from the concrete screens.</summary>
    protected async Task ReloadAsync()
    {
        try
        {
            UseWaitCursor = true;
            var table = await LoadDataAsync();
            ScreenToolbox.TranslateCommonColumns(table);
            _data = table;
            Grid.DataSource = _data;
            ApplySearch();
            Status.Text = $"{ScreenTitle} — إجمالي: {_data.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            _data = null;
            Grid.DataSource = null;
            Status.Text = "تعذر تحميل البيانات: " + ex.GetBaseException().Message;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ApplySearch()
    {
        if (_data is null) return;
        var value = Search.Text.Trim().Replace("'", "''");
        var escaped = value.Replace("%", "[%]").Replace("*", "[*]").Replace("[", "[[]");
        if (string.IsNullOrWhiteSpace(escaped))
        {
            _data.DefaultView.RowFilter = string.Empty;
        }
        else
        {
            var filters = _data.Columns
                .Cast<DataColumn>()
                .Where(c => c.DataType == typeof(string))
                .Select(c => $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{escaped}%'")
                .ToArray();
            _data.DefaultView.RowFilter = filters.Length == 0 ? string.Empty : string.Join(" OR ", filters);
        }
        Status.Text = $"{ScreenTitle} — المعروض: {_data.DefaultView.Count:N0} من {_data.Rows.Count:N0}";
    }

    private void Export() => ScreenToolbox.ExportCsv(this, _data, ScreenTitle);

    private void Print() => ScreenToolbox.ShowPrintPreview(this, ScreenTitle, _data);
}
