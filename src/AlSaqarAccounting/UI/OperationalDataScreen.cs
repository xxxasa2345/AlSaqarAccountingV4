using System.Data;
using System.Net.NetworkInformation;
using System.Text;
using System.Data.SqlClient;

namespace AlSaqarAccounting.UI;

/// <summary>
/// Phase 6.1 first-wave operational screen for simple item-master tables.
/// Existing callers remain compatible because userId is optional.
/// </summary>
public sealed class OperationalDataScreen : Form
{
    private static readonly HashSet<string> WritableMasterTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Item_Company", "Item_Unit", "Item_Class", "Item_Groups"
    };

    private readonly string _connectionString;
    private readonly OperationalScreenDefinition _definition;
    private readonly ScreenAccess _access;
    private readonly int? _branchId;
    private readonly int? _userId;

    private readonly TextBox _search = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, PlaceholderText = "بحث داخل البيانات..." };
    private readonly DataGridView _grid = new()
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
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        Padding = new Padding(8),
        TextAlign = ContentAlignment.MiddleRight,
        BorderStyle = BorderStyle.FixedSingle
    };
    private DataTable? _data;

    private bool CanWrite =>
        !string.IsNullOrWhiteSpace(_definition.TableName) &&
        WritableMasterTables.Contains(_definition.TableName) &&
        _access.AllowEnter;

    public OperationalDataScreen(
        string connectionString,
        OperationalScreenDefinition definition,
        ScreenAccess access,
        int? branchId,
        int? userId = null)
    {
        _connectionString = connectionString;
        _definition = definition;
        _access = access;
        _branchId = branchId;
        _userId = userId;

        Text = "الصقر للمحاسبة — " + definition.DisplayName;
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 650);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildLayout();
        WireEvents();
    }

    private void BuildLayout()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Color.FromArgb(245,247,250), Padding = new Padding(12) };
        var title = new Label { Text = _definition.DisplayName, Dock = DockStyle.Top, Height = 38, Font = new Font("Tahoma", 18, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight };
        var permissions = new Label { Text = BuildPermissionText(), Dock = DockStyle.Top, Height = 26, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleRight };
        var searchPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 32, ColumnCount = 2, RowCount = 1 };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        var refresh = new Button { Text = "تحديث", Dock = DockStyle.Fill, Enabled = _access.AllowEnter };
        refresh.Click += (_, _) => LoadData();
        searchPanel.Controls.Add(_search, 0, 0);
        searchPanel.Controls.Add(refresh, 1, 0);
        header.Controls.Add(searchPanel);
        header.Controls.Add(permissions);
        header.Controls.Add(title);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), WrapContents = false };
        AddToolbarButton(toolbar, "تفاصيل", true, ShowSelectedRecord);
        AddToolbarButton(toolbar, "تصدير CSV", _access.AllowExport, ExportCsv);
        AddToolbarButton(toolbar, "طباعة", _access.AllowPrint, PrintGrid);
        AddToolbarButton(toolbar, "حفظ", CanWrite && _access.AllowSave, SaveNewRecord);
        AddToolbarButton(toolbar, "تعديل", CanWrite && _access.AllowEdit, EditSelectedRecord);
        AddToolbarButton(toolbar, "حذف", CanWrite && _access.AllowDelete, DeleteSelectedRecord);

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(toolbar);
        Controls.Add(header);
        Shown += (_, _) => LoadData();
    }

    private static void AddToolbarButton(FlowLayoutPanel toolbar, string text, bool enabled, Action action)
    {
        var b = new Button { Text = text, Width = 105, Height = 30, Enabled = enabled, Margin = new Padding(4), FlatStyle = FlatStyle.Flat };
        b.Click += (_, _) => action();
        toolbar.Controls.Add(b);
    }

    private string BuildPermissionText()
    {
        var p = new List<string>();
        if (_access.AllowEnter) p.Add("فتح");
        if (_access.AllowSave && CanWrite) p.Add("حفظ");
        if (_access.AllowEdit && CanWrite) p.Add("تعديل");
        if (_access.AllowDelete && CanWrite) p.Add("حذف");
        if (_access.AllowPrint) p.Add("طباعة");
        if (_access.AllowExport) p.Add("تصدير");
        return p.Count == 0 ? "الصلاحيات: لا توجد عمليات متاحة" : "الصلاحيات: " + string.Join("  |  ", p);
    }

    private void WireEvents()
    {
        _search.TextChanged += (_, _) => ApplySearch(_search.Text);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowSelectedRecord(); };
    }

    private void LoadData()
    {
        try
        {
            UseWaitCursor = true;
            _data = _definition.StoredProcedure is not null
                ? LoadStoredProcedure(_definition.StoredProcedure, _definition.PassBranchId)
                : LoadTable(_definition.TableName!);
            TranslateColumns(_data);
            _grid.DataSource = _data;
            ApplySearch(_search.Text);
            _status.Text = BuildStatus();
        }
        catch (Exception ex)
        {
            _data = null;
            _grid.DataSource = null;
            _status.Text = "تعذر تحميل البيانات: " + ex.GetBaseException().Message;
        }
        finally { UseWaitCursor = false; }
    }

    private DataTable LoadStoredProcedure(string procedureName, bool passBranch)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand(procedureName, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 90 };
        if (passBranch)
        {
            if (!_branchId.HasValue) throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");
            command.Parameters.Add("@BranchID", SqlDbType.Int).Value = _branchId.Value;
        }
        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }

    private DataTable LoadTable(string tableName)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Item_Company", "Item_Unit", "Item_Class", "Item_Groups", "Account_CustSup", "Account_Branch"
        };
        if (!allowed.Contains(tableName)) throw new InvalidOperationException("مصدر الشاشة غير موجود في قائمة المصادر المسموح بها.");
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        var safe = tableName.Replace("]", "]]", StringComparison.Ordinal);
        var sql = WritableMasterTables.Contains(tableName) ? $"SELECT ID, Name FROM dbo.[{safe}] ORDER BY ID" : $"SELECT TOP (2000) * FROM dbo.[{safe}]";
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 90 };
        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }

    private void ApplySearch(string? text)
    {
        if (_data is null) return;
        var value = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)) { _data.DefaultView.RowFilter = string.Empty; return; }
        var escaped = value.Replace("'", "''", StringComparison.Ordinal).Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("*", "[*]", StringComparison.Ordinal);
        var filters = _data.Columns.Cast<DataColumn>().Where(c => c.DataType == typeof(string)).Select(c => $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{escaped}%'").ToArray();
        _data.DefaultView.RowFilter = filters.Length == 0 ? string.Empty : string.Join(" OR ", filters);
        _status.Text = BuildStatus();
    }

    private string BuildStatus()
    {
        var shown = _data?.DefaultView.Count ?? 0;
        var total = _data?.Rows.Count ?? 0;
        var source = _definition.StoredProcedure is not null ? "الإجراء: " + _definition.StoredProcedure : "الجدول: " + _definition.TableName;
        return $"{source} — إجمالي: {total:N0} — المعروض: {shown:N0}";
    }

    private int? GetSelectedId()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row || _data is null) return null;
        var key = _definition.KeyColumn ?? "ID";
        if (!_data.Columns.Contains(key)) return null;
        try { return row.Row[key] == DBNull.Value ? null : Convert.ToInt32(row.Row[key]); } catch { return null; }
    }

    private void ShowSelectedRecord()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row)
        {
            MessageBox.Show(this, "حدد سجلًا أولاً.", "تفاصيل", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var details = new RecordDetailsForm(_definition.DisplayName, row.Row, null);
        details.ShowDialog(this);
    }

    private void SaveNewRecord()
    {
        if (!CanWrite || !_access.AllowSave) return;
        using var editor = new MasterNameEditorForm(_definition.DisplayName, "إضافة سجل جديد", string.Empty);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        var name = editor.Value.Trim();
        try { ExecuteInsert(name); LoadData(); SelectByName(name); _status.Text = "تم الحفظ بنجاح: " + name; }
        catch (Exception ex) { MessageBox.Show(this, "تعذر الحفظ:\r\n" + ex.GetBaseException().Message, "حفظ", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void EditSelectedRecord()
    {
        if (!CanWrite || !_access.AllowEdit) return;
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row) { MessageBox.Show(this, "حدد سجلًا أولاً.", "تعديل", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var id = GetSelectedId();
        if (!id.HasValue) { MessageBox.Show(this, "تعذر قراءة معرف السجل المحدد.", "تعديل", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var current = _data?.Columns.Contains("Name") == true ? Convert.ToString(row.Row["Name"]) ?? string.Empty : string.Empty;
        using var editor = new MasterNameEditorForm(_definition.DisplayName, "تعديل السجل", current);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        var name = editor.Value.Trim();
        try { ExecuteUpdate(id.Value, name); LoadData(); SelectById(id.Value); _status.Text = "تم التعديل بنجاح: " + name; }
        catch (Exception ex) { MessageBox.Show(this, "تعذر التعديل:\r\n" + ex.GetBaseException().Message, "تعديل", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void DeleteSelectedRecord()
    {
        if (!CanWrite || !_access.AllowDelete) return;
        var id = GetSelectedId();
        if (!id.HasValue) { MessageBox.Show(this, "حدد سجلًا أولاً.", "حذف", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var name = _grid.CurrentRow?.DataBoundItem is DataRowView row && _data?.Columns.Contains("Name") == true ? Convert.ToString(row.Row["Name"]) ?? string.Empty : string.Empty;
        if (MessageBox.Show(this, $"هل تريد حذف السجل رقم {id.Value}؟\r\nالاسم: {name}", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try { ExecuteDelete(id.Value); LoadData(); _status.Text = "تم حذف السجل رقم " + id.Value; }
        catch (Exception ex) { MessageBox.Show(this, "تعذر الحذف:\r\n" + ex.GetBaseException().Message, "حذف", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ExecuteInsert(string name)
    {
        using var cn = new SqlConnection(_connectionString); cn.Open();
        var table = _definition.TableName!; var safe = table.Replace("]", "]]", StringComparison.Ordinal);
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"INSERT INTO dbo.[{safe}] (Name, UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add) VALUES (@Name, @UserID, @BranchID, @Mac, GETDATE());";
        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 300).Value = name;
        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = _userId ?? 0;
        cmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = _branchId ?? 0;
        cmd.Parameters.Add("@Mac", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        cmd.ExecuteNonQuery();
    }

    private void ExecuteUpdate(int id, string name)
    {
        using var cn = new SqlConnection(_connectionString); cn.Open();
        var table = _definition.TableName!; var safe = table.Replace("]", "]]", StringComparison.Ordinal);
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"UPDATE dbo.[{safe}] SET Name=@Name, UserID_Update=@UserID, UserBranch_Update=@BranchID, UserMacAddress_Update=@Mac, UserDate_Update=GETDATE() WHERE ID=@ID;";
        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 300).Value = name;
        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = _userId ?? 0;
        cmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = _branchId ?? 0;
        cmd.Parameters.Add("@Mac", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;
        cmd.ExecuteNonQuery();
    }

    private void ExecuteDelete(int id)
    {
        using var cn = new SqlConnection(_connectionString); cn.Open();
        var table = _definition.TableName!; var safe = table.Replace("]", "]]", StringComparison.Ordinal);
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"DELETE FROM dbo.[{safe}] WHERE ID=@ID;";
        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;
        cmd.ExecuteNonQuery();
    }

    private void SelectById(int id)
    {
        var key = _definition.KeyColumn ?? "ID";
        if (!_grid.Columns.Contains(key)) return;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            try { if (row.Cells[key].Value != null && Convert.ToInt32(row.Cells[key].Value) == id) { row.Selected = true; _grid.CurrentCell = row.Cells[0]; return; } } catch { }
        }
    }

    private void SelectByName(string name)
    {
        if (_grid.Columns.Contains("Name"))
            foreach (DataGridViewRow row in _grid.Rows)
                if (string.Equals(Convert.ToString(row.Cells["Name"].Value), name, StringComparison.OrdinalIgnoreCase)) { row.Selected = true; _grid.CurrentCell = row.Cells["Name"]; return; }
    }

    private static string GetMachineMac()
    {
        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up).Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback).Select(n => n.GetPhysicalAddress()?.ToString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return string.IsNullOrWhiteSpace(mac) ? Environment.MachineName : mac;
        }
        catch { return Environment.MachineName; }
    }

    private void ExportCsv()
    {
        if (!_access.AllowExport || _data is null) return;
        using var dialog = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = SanitizeFileName(_definition.DisplayName) + ".csv", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var sb = new StringBuilder();
        for (var i = 0; i < _data.Columns.Count; i++) { if (i > 0) sb.Append(','); sb.Append(EscapeCsv(_data.Columns[i].Caption)); }
        sb.AppendLine();
        foreach (DataRowView view in _data.DefaultView) { for (var i = 0; i < _data.Columns.Count; i++) { if (i > 0) sb.Append(','); sb.Append(EscapeCsv(view.Row[i] == DBNull.Value ? string.Empty : Convert.ToString(view.Row[i]) ?? string.Empty)); } sb.AppendLine(); }
        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
        _status.Text = "تم التصدير: " + dialog.FileName;
    }

    private void PrintGrid()
    {
        if (!_access.AllowPrint) return;
        using var preview = new Form { Text = "معاينة الطباعة — " + _definition.DisplayName, Width = 1100, Height = 750, StartPosition = FormStartPosition.CenterParent, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
        var copy = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, RightToLeft = RightToLeft.Yes, DataSource = _data?.DefaultView.ToTable() };
        var close = new Button { Text = "إغلاق", Dock = DockStyle.Bottom, Height = 38 }; close.Click += (_, _) => preview.Close();
        preview.Controls.Add(copy); preview.Controls.Add(close); preview.ShowDialog(this);
    }

    private static string EscapeCsv(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n') ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    private static string SanitizeFileName(string value) { foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_'); return string.IsNullOrWhiteSpace(value) ? "export" : value; }

    private static void TranslateColumns(DataTable table)
    {
        var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { ["ID"]="المعرف", ["ItemId"]="رقم الصنف", ["Item_code"]="كود الصنف", ["item_Name"]="اسم الصنف", ["Account_No"]="رقم الحساب", ["Account_Name"]="اسم الحساب", ["BranchName"]="اسم الفرع", ["SupplierName"]="اسم العميل/المورد", ["Purchases_Date"]="التاريخ", ["Net"]="الصافي", ["Tax"]="الضريبة", ["TotalPrices"]="الإجمالي", ["Note"]="الملاحظات", ["Name"]="الاسم" };
        foreach (DataColumn c in table.Columns) if (map.TryGetValue(c.ColumnName, out var a)) c.Caption = a;
    }
}

internal sealed class RecordDetailsForm : Form
{
    public RecordDetailsForm(string title, DataRow row, object? key)
    {
        Text = "تفاصيل — " + title; Width = 720; Height = 680; StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, RightToLeft = RightToLeft.Yes };
        grid.Columns.Add("Field", "الحقل"); grid.Columns.Add("Value", "القيمة");
        foreach (DataColumn c in row.Table.Columns) grid.Rows.Add(c.Caption, row[c] == DBNull.Value ? string.Empty : Convert.ToString(row[c]) ?? string.Empty);
        Controls.Add(grid);
    }
}

internal sealed class MasterNameEditorForm : Form
{
    private readonly TextBox _nameBox = new() { Dock = DockStyle.Fill };
    public string Value => _nameBox.Text;
    public MasterNameEditorForm(string screenName, string operation, string initialValue)
    {
        Text = $"{operation} — {screenName}"; Width = 520; Height = 180; StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; Padding = new Padding(14);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "الاسم:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
        _nameBox.Text = initialValue; layout.Controls.Add(_nameBox, 1, 0);
        var ok = new Button { Text = "موافق", Width = 100, Height = 32 }; var cancel = new Button { Text = "إلغاء", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => { if (string.IsNullOrWhiteSpace(_nameBox.Text)) { MessageBox.Show(this, "أدخل الاسم.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning); _nameBox.Focus(); return; } DialogResult = DialogResult.OK; Close(); };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false }; buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 1, 2); Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
    }
}
