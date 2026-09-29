using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Real ERP form for Store management (Account_Stores).
/// </summary>
public sealed class StoresForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly StoreService _service;
    
    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new();
    private readonly TextBox _storeName = new();
    private readonly TextBox _phone = new();
    private readonly TextBox _fax = new();
    private readonly TextBox _address = new();
    private readonly Label _status = new();
    
    private DataTable? _data;
    private int? _editingId;

    public StoresForm(AppSession session, ScreenAccess access, StoreService service)
    {
        _session = session;
        _access = access;
        _service = service;
        
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = " "; // "المخازن - إدارة المخازن"
        Width = 1200;
        Height = 750;
        MinimumSize = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label
        {
            Text = "", // "إدارة المخازن"
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $": {_session.UserName} | : {_session.BranchId?.ToString() ?? "-"}", // "المستخدم: ... | الفرع: ..."
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 32, ColumnCount = 2 };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        var refresh = new Button { Text = "", Dock = DockStyle.Fill, Enabled = _access.AllowEnter }; // "تحديث"
        refresh.Click += async (_, _) => await LoadAsync();
        _search.Dock = DockStyle.Fill;
        _search.RightToLeft = RightToLeft.Yes;
        searchRow.Controls.Add(_search, 0, 0);
        searchRow.Controls.Add(refresh, 1, 0);
        header.Controls.Add(searchRow);
        header.Controls.Add(info);
        header.Controls.Add(title);

        // Editor Panel
        var editor = new TableLayoutPanel { Dock = DockStyle.Top, Height = 160, ColumnCount = 4, RowCount = 2, Padding = new Padding(8) };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(editor, "", _storeName, 0, 0); // "اسم المخزن"
        AddField(editor, "", _phone, 1, 0); // "الهاتف"
        AddField(editor, "", _fax, 2, 0); // "الفاكس"
        AddField(editor, "", _address, 0, 1, true); // "العنوان"
        editor.SetColumnSpan(_address, 3);

        var save = new Button { Text = "", Width = 100, Height = 32, Enabled = _access.AllowSave }; // "حفظ"
        var cancel = new Button { Text = "", Width = 100, Height = 32 }; // "إلغاء"
        save.Click += async (_, _) => await SaveAsync();
        cancel.Click += (_, _) => ClearEditor();
        editor.Controls.Add(save, 2, 2);
        editor.Controls.Add(cancel, 3, 2);

        // Toolbar
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), WrapContents = false };
        AddToolbarButton(toolbar, "", _access.AllowEdit, BeginEditAsync); // "تعديل"
        AddToolbarButton(toolbar, "", _access.AllowDelete, DeleteAsync); // "حذف"
        AddToolbarButton(toolbar, "", _access.AllowExport, ExportCsv); // "تصدير CSV"

        // Grid
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _grid.RightToLeft = RightToLeft.Yes;
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && _access.AllowEdit) await BeginEditAsync(); };

        // Status
        _status.Dock = DockStyle.Bottom;
        _status.Height = 32;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Padding = new Padding(8);
        _status.BorderStyle = BorderStyle.FixedSingle;

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(toolbar);
        Controls.Add(editor);
        Controls.Add(header);

        _search.TextChanged += (_, _) => ApplyFilter();
        Shown += async (_, _) => await LoadAsync();
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control, int column, int row, bool multiLine = false)
    {
        var box = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var text = new Label { Text = label, Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleRight };
        control.Dock = DockStyle.Fill;
        control.RightToLeft = RightToLeft.Yes;
        if (multiLine && control is TextBox tb)
        {
            tb.Multiline = true;
            tb.Height = 60;
        }
        box.Controls.Add(control);
        box.Controls.Add(text);
        panel.Controls.Add(box, column, row);
    }

    private static void AddToolbarButton(FlowLayoutPanel panel, string text, bool enabled, Func<Task> action)
    {
        var button = new Button { Text = text, Width = 100, Height = 30, Enabled = enabled, Margin = new Padding(4) };
        button.Click += async (_, _) => await action();
        panel.Controls.Add(button);
    }

    private async Task LoadAsync()
    {
        try
        {
            UseWaitCursor = true;
            _data = await _service.ListAsync(_session.BranchId);
            _grid.DataSource = _data;
            FormatGrid();
            ApplyFilter();
            _status.Text = $": {_data.Rows.Count:N0}"; // "عدد المخازن: ..."
        }
        catch (Exception ex)
        {
            _status.Text = ": " + ex.GetBaseException().Message; // "خطأ في تحميل البيانات: ..."
        }
        finally { UseWaitCursor = false; }
    }

    private void FormatGrid()
    {
        if (_data == null || _grid.Columns.Count == 0) return;
        
        // Hide unnecessary columns
        var hiddenColumns = new[] { "ID", "BranchID", "UserID_Add", "UserBranch_Add", "UserMacAddress_Add", "UserDate_Add", 
                                     "UserID_Update", "UserBranch_Update", "UserMacAddress_Update", "UserDate_Update" };
        
        foreach (var colName in hiddenColumns)
        {
            if (_grid.Columns.Contains(colName))
                _grid.Columns[colName].Visible = false;
        }
        
        // Rename columns
        if (_grid.Columns.Contains("Store_Name")) _grid.Columns["Store_Name"].HeaderText = ""; // "اسم المخزن"
        if (_grid.Columns.Contains("Phone")) _grid.Columns["Phone"].HeaderText = ""; // "الهاتف"
        if (_grid.Columns.Contains("Fax")) _grid.Columns["Fax"].HeaderText = ""; // "الفاكس"
        if (_grid.Columns.Contains("Address")) _grid.Columns["Address"].HeaderText = ""; // "العنوان"
    }

    private void ApplyFilter()
    {
        if (_data is null) return;
        var term = _search.Text.Trim().Replace("'", "''");
        if (string.IsNullOrWhiteSpace(term))
        {
            _data.DefaultView.RowFilter = string.Empty;
        }
        else
        {
            _data.DefaultView.RowFilter = $"CONVERT([Store_Name], 'System.String') LIKE '%{term}%' OR " +
                                   $"CONVERT([Phone], 'System.String') LIKE '%{term}%' OR " +
                                   $"CONVERT([Fax], 'System.String') LIKE '%{term}%' OR " +
                                   $"CONVERT([Address], 'System.String') LIKE '%{term}%';";
        }
        _status.Text = $": {_data.DefaultView.Count:N0}  {_data.Rows.Count:N0}"; // "المعرض: ... من ..."
    }

    private async Task SaveAsync()
    {
        if (!_access.AllowSave) return;
        
        var store = new Account_Stores
        {
            Store_Name = _storeName.Text.Trim(),
            Phone = _phone.Text.Trim(),
            Fax = _fax.Text.Trim(),
            Address = _address.Text.Trim(),
            BranchID = _session.BranchId
        };

        try
        {
            UseWaitCursor = true;
            if (_editingId.HasValue)
            {
                if (!_access.AllowEdit) return;
                store.ID = _editingId.Value;
                await _service.UpdateAsync(store, _session);
                _status.Text = "  "; // "تم تعديل المخزن بنجاح"
            }
            else
            {
                var id = await _service.CreateAsync(store, _session);
                _status.Text = $"   {id}"; // "تم حفظ المخزن بنجاح رقم: ..."
            }
            ClearEditor();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ"
        }
        finally { UseWaitCursor = false; }
    }

    private Task BeginEditAsync()
    {
        if (!_access.AllowEdit || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return Task.CompletedTask;
        
        _editingId = Convert.ToInt32(row.Row["ID"]);
        _storeName.Text = Convert.ToString(row.Row["Store_Name"]) ?? string.Empty;
        _phone.Text = Convert.ToString(row.Row["Phone"]) ?? string.Empty;
        _fax.Text = Convert.ToString(row.Row["Fax"]) ?? string.Empty;
        _address.Text = Convert.ToString(row.Row["Address"]) ?? string.Empty;
        
        _storeName.Focus();
        return Task.CompletedTask;
    }

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        var id = Convert.ToInt32(row.Row["ID"]);
        var name = Convert.ToString(row.Row["Store_Name"]) ?? string.Empty;
        
        if (MessageBox.Show(this, $" {id}\r\n{name}", "", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) // "هل تريد حذف المخزن؟"
            return;
        
        try
        {
            UseWaitCursor = true;
            await _service.DeleteAsync(id);
            await LoadAsync();
            _status.Text = " "; // "تم حذف المخزن"
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ في الحذف"
        }
        finally { UseWaitCursor = false; }
    }

    private void ClearEditor()
    {
        _editingId = null;
        _storeName.Clear();
        _phone.Clear();
        _fax.Clear();
        _address.Clear();
    }

    private Task ExportCsv()
    {
        if (!_access.AllowExport || _data is null) return Task.CompletedTask;
        using var dialog = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = "Stores.csv", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;
        
        var sb = new System.Text.StringBuilder();
        // Header
        var headers = new[] { "", "", "", "" }; // "اسم المخزن", "الهاتف", "الفاكس", "العنوان"
        sb.AppendLine(string.Join(",", headers));
        
        // Data
        foreach (DataRowView view in _data.DefaultView)
        {
            var values = new[]
            {
                view.Row["Store_Name"]?.ToString() ?? string.Empty,
                view.Row["Phone"]?.ToString() ?? string.Empty,
                view.Row["Fax"]?.ToString() ?? string.Empty,
                view.Row["Address"]?.ToString() ?? string.Empty
            };
            sb.AppendLine(string.Join(",", values.Select(v => EscapeCsv(v))));
        }
        
        File.WriteAllText(dialog.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
        _status.Text = ": " + dialog.FileName; // "تم التصدير إلى: ..."
        return Task.CompletedTask;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
