using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Real ERP form for Contract management (Contract_Contract).
/// </summary>
public sealed class ContractsForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly ContractService _service;
    
    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new();
    private readonly TextBox _supplierName = new();
    private readonly TextBox _supplierPhone = new();
    private readonly TextBox _contractNum = new();
    private readonly DateTimePicker _purchaseDate = new();
    private readonly TextBox _costOrder = new();
    private readonly TextBox _tax = new();
    private readonly TextBox _totalPrices = new();
    private readonly TextBox _net = new();
    private readonly TextBox _note = new();
    private readonly Label _status = new();
    
    private DataTable? _data;
    private int? _editingId;

    public ContractsForm(AppSession session, ScreenAccess access, ContractService service)
    {
        _session = session;
        _access = access;
        _service = service;
        
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = "العقود - إدارة العقود"; // "العقود - إدارة العقود"
        Width = 1400;
        Height = 850;
        MinimumSize = new Size(1200, 750);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label
        {
            Text = "إدارة العقود", // "إدارة العقود"
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
        var refresh = new Button { Text = "تحديث", Dock = DockStyle.Fill, Enabled = _access.AllowEnter }; // "تحديث"
        refresh.Click += async (_, _) => await LoadAsync();
        _search.Dock = DockStyle.Fill;
        _search.RightToLeft = RightToLeft.Yes;
        searchRow.Controls.Add(_search, 0, 0);
        searchRow.Controls.Add(refresh, 1, 0);
        header.Controls.Add(searchRow);
        header.Controls.Add(info);
        header.Controls.Add(title);

        // Editor Panel
        var editor = new TableLayoutPanel { Dock = DockStyle.Top, Height = 220, ColumnCount = 4, RowCount = 4, Padding = new Padding(8) };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(editor, "اسم المورد", _supplierName, 0, 0); // "اسم المورد"
        AddField(editor, "هاتف المورد", _supplierPhone, 1, 0); // "هاتف المورد"
        AddField(editor, "رقم العقد", _contractNum, 2, 0); // "رقم العقد"
        AddField(editor, "تاريخ الشراء", _purchaseDate, 3, 0); // "تاريخ الشراء"
        AddField(editor, "تكلفة الطلب", _costOrder, 0, 1); // "تكلفة الطلب"
        AddField(editor, "الضريبة", _tax, 1, 1); // "الضريبة"
        AddField(editor, "الإجمالي", _totalPrices, 2, 1); // "الإجمالي"
        AddField(editor, "الصافي", _net, 3, 1); // "الصافي"
        AddField(editor, "ملاحظات", _note, 0, 2, true); // "ملاحظات"
        editor.SetColumnSpan(_note, 3);

        var save = new Button { Text = "حفظ", Width = 100, Height = 32, Enabled = _access.AllowSave || _access.AllowEdit }; // "حفظ"
        var cancel = new Button { Text = "إلغاء", Width = 100, Height = 32 }; // "إلغاء"
        save.Click += async (_, _) => await SaveAsync();
        cancel.Click += (_, _) => ClearEditor();
        editor.Controls.Add(save, 2, 3);
        editor.Controls.Add(cancel, 3, 3);

        // Toolbar
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), WrapContents = false };
        AddToolbarButton(toolbar, "تعديل", _access.AllowEdit, BeginEditAsync); // "تعديل"
        AddToolbarButton(toolbar, "حذف", _access.AllowDelete, DeleteAsync); // "حذف"
        AddToolbarButton(toolbar, "تصدير CSV", _access.AllowExport, ExportCsv); // "تصدير CSV"

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
        ErpTheme.ConfigureGrid(_grid);

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
        if (control is DateTimePicker dtp)
        {
            dtp.Format = DateTimePickerFormat.Short;
            dtp.RightToLeft = RightToLeft.Yes;
            dtp.RightToLeftLayout = true;
        }
        if (control is TextBox txt && !multiLine)
        {
            txt.RightToLeft = RightToLeft.Yes;
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
            _status.Text = $": {_data.Rows.Count:N0}"; // "عدد العقود: ..."
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
        var hiddenColumns = new[] { "ID", "PurBranchID", "BranchID", "SupplierID", "SupplierVatNum", 
                                     "UserID_Add", "UserBranch_Add", "UserMacAddress_Add", "UserDate_Add",
                                     "UserID_Update", "UserBranch_Update", "UserMacAddress_Update", "UserDate_Update",
                                     "BounceID", "contractTerms", "Safy", "DiscountNum", "DiscountPerantage",
                                     "Tax_Discount", "TotalPrices_Discount", "AllTax" };
        
        foreach (var colName in hiddenColumns)
        {
            if (_grid.Columns.Contains(colName))
                _grid.Columns[colName].Visible = false;
        }
        
        // Rename columns
        if (_grid.Columns.Contains("SupplierName")) _grid.Columns["SupplierName"].HeaderText = "اسم المورد"; // "اسم المورد"
        if (_grid.Columns.Contains("SupplierPhone")) _grid.Columns["SupplierPhone"].HeaderText = "هاتف المورد"; // "هاتف المورد"
        if (_grid.Columns.Contains("NoteNum")) _grid.Columns["NoteNum"].HeaderText = "رقم العقد"; // "رقم العقد"
        if (_grid.Columns.Contains("Purchases_Date")) _grid.Columns["Purchases_Date"].HeaderText = "تاريخ الشراء"; // "تاريخ الشراء"
        if (_grid.Columns.Contains("CostOrder")) _grid.Columns["CostOrder"].HeaderText = "تكلفة الطلب"; // "تكلفة الطلب"
        if (_grid.Columns.Contains("Tax")) _grid.Columns["Tax"].HeaderText = "الضريبة"; // "الضريبة"
        if (_grid.Columns.Contains("TotalPrices")) _grid.Columns["TotalPrices"].HeaderText = "الإجمالي"; // "الإجمالي"
        if (_grid.Columns.Contains("Net")) _grid.Columns["Net"].HeaderText = "الصافي"; // "الصافي"
        if (_grid.Columns.Contains("Note")) _grid.Columns["Note"].HeaderText = "ملاحظات"; // "ملاحظات"
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
            _data.DefaultView.RowFilter = $"CONVERT([SupplierName], 'System.String') LIKE '%{term}%' OR " +
                                   $"CONVERT([NoteNum], 'System.String') LIKE '%{term}%' OR " +
                                   $"CONVERT([SupplierPhone], 'System.String') LIKE '%{term}%' OR " +
                                   $"CONVERT([Note], 'System.String') LIKE '%{term}%';";
        }
        _status.Text = $": {_data.DefaultView.Count:N0}  {_data.Rows.Count:N0}"; // "المعرض: ... من ..."
    }

    private async Task SaveAsync()
    {
        if (_editingId.HasValue)
        {
            if (!_access.AllowEdit) return;
        }
        else if (!_access.AllowSave)
        {
            return;
        }
        
        var contract = new Contract_Contract
        {
            SupplierName = _supplierName.Text.Trim(),
            SupplierPhone = _supplierPhone.Text.Trim(),
            NoteNum = _contractNum.Text.Trim(),
            Purchases_Date = _purchaseDate.Value,
            CostOrder = decimal.TryParse(_costOrder.Text, out var cost) ? cost : 0,
            Tax = decimal.TryParse(_tax.Text, out var taxVal) ? taxVal : 0,
            TotalPrices = decimal.TryParse(_totalPrices.Text, out var total) ? total : 0,
            Net = decimal.TryParse(_net.Text, out var netVal) ? netVal : 0,
            Note = _note.Text.Trim(),
            BranchID = _session.BranchId
        };

        try
        {
            UseWaitCursor = true;
            if (_editingId.HasValue)
            {
                contract.ID = _editingId.Value;
                await _service.UpdateAsync(contract, _session);
                _status.Text = "  "; // "تم تعديل العقد بنجاح"
            }
            else
            {
                var id = await _service.CreateAsync(contract, _session);
                _status.Text = $"   {id}"; // "تم حفظ العقد بنجاح رقم: ..."
            }
            ClearEditor();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ"
        }
        finally { UseWaitCursor = false; }
    }

    private Task BeginEditAsync()
    {
        if (!_access.AllowEdit || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return Task.CompletedTask;
        
        _editingId = Convert.ToInt32(row.Row["ID"]);
        _supplierName.Text = Convert.ToString(row.Row["SupplierName"]) ?? string.Empty;
        _supplierPhone.Text = Convert.ToString(row.Row["SupplierPhone"]) ?? string.Empty;
        _contractNum.Text = Convert.ToString(row.Row["NoteNum"]) ?? string.Empty;
        _purchaseDate.Value = row.Row["Purchases_Date"] != DBNull.Value ? Convert.ToDateTime(row.Row["Purchases_Date"]) : DateTime.Now;
        _costOrder.Text = Convert.ToString(row.Row["CostOrder"]) ?? string.Empty;
        _tax.Text = Convert.ToString(row.Row["Tax"]) ?? string.Empty;
        _totalPrices.Text = Convert.ToString(row.Row["TotalPrices"]) ?? string.Empty;
        _net.Text = Convert.ToString(row.Row["Net"]) ?? string.Empty;
        _note.Text = Convert.ToString(row.Row["Note"]) ?? string.Empty;
        
        _supplierName.Focus();
        return Task.CompletedTask;
    }

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        var id = Convert.ToInt32(row.Row["ID"]);
        var name = Convert.ToString(row.Row["SupplierName"]) ?? string.Empty;
        
        if (MessageBox.Show(this, $" {id}\r\n{name}", "هل تريد حذف العقد؟", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) // "هل تريد حذف العقد؟"
            return;
        
        try
        {
            UseWaitCursor = true;
            await _service.DeleteAsync(id);
            await LoadAsync();
            _status.Text = " "; // "تم حذف العقد"
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "خطأ في الحذف", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ في الحذف"
        }
        finally { UseWaitCursor = false; }
    }

    private void ClearEditor()
    {
        _editingId = null;
        _supplierName.Clear();
        _supplierPhone.Clear();
        _contractNum.Clear();
        _purchaseDate.Value = DateTime.Now;
        _costOrder.Clear();
        _tax.Clear();
        _totalPrices.Clear();
        _net.Clear();
        _note.Clear();
    }

    private Task ExportCsv()
    {
        if (!_access.AllowExport || _data is null) return Task.CompletedTask;
using var dialog = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = "Contracts.csv", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;
var sb = new System.Text.StringBuilder();
        // Header
        var headers = new[] { "", "", "", "", "", "", "", "" }; 
        // "رقم العقد", "اسم المورد", "هاتف المورد", "تاريخ الشراء", "تكلفة الطلب", "الضريبة", "الإجمالي", "الصافي"
        sb.AppendLine(string.Join(",", headers));
        
        // Data
        foreach (DataRowView view in _data.DefaultView)
        {
            var values = new[]
            {
                view.Row["NoteNum"]?.ToString() ?? string.Empty,
                view.Row["SupplierName"]?.ToString() ?? string.Empty,
                view.Row["SupplierPhone"]?.ToString() ?? string.Empty,
                view.Row["Purchases_Date"]?.ToString() ?? string.Empty,
                view.Row["CostOrder"]?.ToString() ?? string.Empty,
                view.Row["Tax"]?.ToString() ?? string.Empty,
                view.Row["TotalPrices"]?.ToString() ?? string.Empty,
                view.Row["Net"]?.ToString() ?? string.Empty
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
