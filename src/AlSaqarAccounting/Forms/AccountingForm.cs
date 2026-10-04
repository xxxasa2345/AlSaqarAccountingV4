using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Real ERP Form for Chart of Accounts and General Ledger.
/// </summary>
public sealed class AccountingForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly AccountingService _service;
    
    private readonly DataGridView _accountsGrid = new();
    private readonly DataGridView _costCentersGrid = new();
    private readonly TextBox _searchText = new();
    private readonly TextBox _accountNoText = new();
    private readonly TextBox _accountNameText = new();
    private readonly TextBox _englishNameText = new();
    private readonly ComboBox _accountTypeCombo = new();
    private readonly ComboBox _accountNatureCombo = new();
    private readonly TextBox _mainAccountText = new();
    private readonly TextBox _phoneText = new();
    private readonly TextBox _addressText = new();
    private readonly TextBox _notesText = new();
    private readonly Label _status = new();
    
    private DataTable? _accountsData;
    private DataTable? _costCentersData;
    private int? _selectedAccountId;
    private bool _accountsMode = true;

    public AccountingForm(AppSession session, ScreenAccess access, AccountingService service)
    {
        _session = session;
        _access = access;
        _service = service;
        
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = "النظام المحاسبي - شجرة الحسابات"; // "النظام المحاسبي - شجرة الحسابات"
        Width = 1400;
        Height = 900;
        MinimumSize = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        // Header Panel
        var header = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label
        {
            Text = "شجرة الحسابات", // "شجرة الحسابات"
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"المستخدم: ... | الفرع: ..."-"}", // "المستخدم: ... | الفرع: ..."
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(title);
        header.Controls.Add(info);

        // Mode Toggle Panel
        var modePanel = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        var modeLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        
        var accountsBtn = new RadioButton { Text = "الحسابات", Width = 120, Checked = true }; // "الحسابات"
        accountsBtn.CheckedChanged += (_, _) => { _accountsMode = true; LoadData(); };
        
        var costCentersBtn = new RadioButton { Text = "مراكز التكلفة", Width = 120 }; // "مراكز التكلفة"
        costCentersBtn.CheckedChanged += (_, _) => { _accountsMode = false; LoadData(); };
        
        modeLayout.Controls.Add(accountsBtn);
        modeLayout.Controls.Add(costCentersBtn);
        modePanel.Controls.Add(modeLayout);

        // Filter Panel
        var filterPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        
        var filterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        
        var searchLabel = new Label { Text = "بحث:", TextAlign = ContentAlignment.MiddleRight }; // "بحث:"
        _searchText.Dock = DockStyle.Fill;
        _searchText.RightToLeft = RightToLeft.Yes;
        
        var refreshBtn = new Button { Text = "تحديث", Width = 80, Height = 30 }; // "تحديث"
        refreshBtn.Click += (_, _) => LoadData();
        
        filterLayout.Controls.Add(searchLabel, 0, 0);
        filterLayout.Controls.Add(_searchText, 1, 0);
        filterLayout.Controls.Add(refreshBtn, 2, 0);
        filterPanel.Controls.Add(filterLayout);

        // Main Grid
        var gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var gridTitle = new Label { Text = "قائمة الحسابات", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "قائمة الحسابات"
        
        _accountsGrid.Dock = DockStyle.Fill;
        _accountsGrid.ReadOnly = true;
        _accountsGrid.AllowUserToAddRows = false;
        _accountsGrid.AllowUserToDeleteRows = false;
        _accountsGrid.AutoGenerateColumns = true;
        _accountsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _accountsGrid.RightToLeft = RightToLeft.Yes;
        _accountsGrid.RowHeadersVisible = false;
        _accountsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _accountsGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) LoadAccountDetails(); };
        
        _costCentersGrid.Dock = DockStyle.Fill;
        _costCentersGrid.ReadOnly = true;
        _costCentersGrid.AllowUserToAddRows = false;
        _costCentersGrid.AllowUserToDeleteRows = false;
        _costCentersGrid.AutoGenerateColumns = true;
        _costCentersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _costCentersGrid.RightToLeft = RightToLeft.Yes;
        _costCentersGrid.RowHeadersVisible = false;
        _costCentersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _costCentersGrid.Visible = false;
        
        gridPanel.Controls.Add(_accountsGrid);
        gridPanel.Controls.Add(_costCentersGrid);
        gridPanel.Controls.Add(gridTitle);

        // Details Panel
        var detailsPanel = new Panel { Dock = DockStyle.Bottom, Height = 250, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        var detailsTitle = new Label { Text = "تفاصيل الحساب", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "تفاصيل الحساب"
        
        var detailsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(4) };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(detailsLayout, "رقم الحساب", _accountNoText, 0, 0); // "رقم الحساب"
        AddField(detailsLayout, "اسم الحساب", _accountNameText, 1, 0); // "اسم الحساب"
        AddField(detailsLayout, "الاسم الانجليزي", _englishNameText, 2, 0); // "الاسم الانجليزي"
        AddField(detailsLayout, "الحساب الرئيسي", _mainAccountText, 3, 0); // "الحساب الرئيسي"
        AddField(detailsLayout, "نوع الحساب", _accountTypeCombo, 0, 1); // "نوع الحساب"
        AddField(detailsLayout, "طبيعة الحساب", _accountNatureCombo, 1, 1); // "طبيعة الحساب"
        AddField(detailsLayout, "الهاتف", _phoneText, 2, 1); // "الهاتف"
        AddField(detailsLayout, "العنوان", _addressText, 3, 1); // "العنوان"
        AddField(detailsLayout, "ملاحظات", _notesText, 0, 2, true); // "ملاحظات"
        detailsLayout.SetColumnSpan(_notesText, 3);

        detailsPanel.Controls.Add(detailsLayout);
        detailsPanel.Controls.Add(detailsTitle);

        // Toolbar Panel
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), WrapContents = false, BackColor = Color.FromArgb(240, 248, 255) };
        
        AddToolbarButton(toolbar, "حفظ", _access.AllowSave, SaveAccount); // "حفظ"
        AddToolbarButton(toolbar, "تعديل", _access.AllowEdit, EditAccount); // "تعديل"
        AddToolbarButton(toolbar, "حذف", _access.AllowDelete, DeleteAccount); // "حذف"
        AddToolbarButton(toolbar, "تصدير", _access.AllowExport, ExportAccounts); // "تصدير"
        AddToolbarButton(toolbar, "ميزان المراجعة", true, ShowTrialBalance); // "ميزان المراجعة"

        // Status Bar
        _status.Dock = DockStyle.Bottom;
        _status.Height = 32;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Padding = new Padding(8);
        _status.BorderStyle = BorderStyle.FixedSingle;
        _status.BackColor = Color.FromArgb(220, 230, 241);

        Controls.Add(header);
        Controls.Add(modePanel);
        Controls.Add(filterPanel);
        Controls.Add(gridPanel);
        Controls.Add(detailsPanel);
        Controls.Add(toolbar);
        Controls.Add(_status);

        // Initialize combos
        InitializeCombos();
        
        // Event handlers
        _searchText.TextChanged += (_, _) => ApplyFilter();
        Shown += async (_, _) => await LoadInitialDataAsync();
    }

    private static void AddToolbarButton(FlowLayoutPanel panel, string text, bool enabled, Action action)
    {
        var button = new Button { Text = text, Width = 100, Height = 36, Enabled = enabled, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
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

    private void InitializeCombos()
    {
        _accountTypeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _accountTypeCombo.Items.AddRange(new object[] { "\u0001", "\u0001", "\u0001", "\u0001" }); // "أصول", "خصوم", "إيرادات", "مصروفات"
        
        _accountNatureCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _accountNatureCombo.Items.AddRange(new object[] { "\u0001", "\u0001" }); // "مدين", "دائن"
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            UseWaitCursor = true;
            await LoadData();
            _status.Text = "النظام جاهز"; // "النظام جاهز"
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ: " + ex.GetBaseException().Message; // "خطأ: "
        }
        finally { UseWaitCursor = false; }
    }

    private async Task LoadData()
    {
        try
        {
            UseWaitCursor = true;
            
            if (_accountsMode)
            {
                _accountsGrid.Visible = true;
                _costCentersGrid.Visible = false;
                _accountsData = await _service.GetChartOfAccountsAsync(_session.BranchId);
                _accountsGrid.DataSource = _accountsData;
                FormatAccountsGrid();
            }
            else
            {
                _accountsGrid.Visible = false;
                _costCentersGrid.Visible = true;
                _costCentersData = await _service.GetCostCentersAsync(_session.BranchId);
                _costCentersGrid.DataSource = _costCentersData;
                FormatCostCentersGrid();
            }
            
            ApplyFilter();
            ClearDetails();
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ في التحميل: " + ex.GetBaseException().Message; // "خطأ في التحميل: "
        }
        finally { UseWaitCursor = false; }
    }

    private void FormatAccountsGrid()
    {
        if (_accountsGrid.Columns.Count == 0) return;
        
        // Hide unnecessary columns
        var hiddenColumns = new[] { "ID", "Suspended", "Account_Level", "Final_Account", "Main_Account_No", 
                                     "BranchID", "Priv_Debit", "Priv_Credit", "UserID_Add", "UserBranch_Add",
                                     "UserMacAddress_Add", "UserDate_Add", "UserID_Update", "UserBranch_Update",
                                     "UserMacAddress_Update", "UserDate_Update", "Date_OpenCharge" };
        
        foreach (var colName in hiddenColumns)
        {
            if (_accountsGrid.Columns.Contains(colName))
                _accountsGrid.Columns[colName].Visible = false;
        }
        
        // Rename columns
        if (_accountsGrid.Columns.Contains("Account_No")) _accountsGrid.Columns["Account_No"].HeaderText = "رقم الحساب"; // "رقم الحساب"
        if (_accountsGrid.Columns.Contains("Account_Name")) _accountsGrid.Columns["Account_Name"].HeaderText = "اسم الحساب"; // "اسم الحساب"
        if (_accountsGrid.Columns.Contains("E_Account_Name")) _accountsGrid.Columns["E_Account_Name"].HeaderText = "الاسم الانجليزي"; // "الاسم الانجليزي"
        if (_accountsGrid.Columns.Contains("Account_Type")) _accountsGrid.Columns["Account_Type"].HeaderText = "نوع الحساب"; // "نوع الحساب"
        if (_accountsGrid.Columns.Contains("Account_Nature")) _accountsGrid.Columns["Account_Nature"].HeaderText = "طبيعة الحساب"; // "طبيعة الحساب"
    }

    private void FormatCostCentersGrid()
    {
        if (_costCentersGrid.Columns.Count == 0) return;
        
        // Hide unnecessary columns
        var hiddenColumns = new[] { "SN", "MainCostCentersID", "Priv_Credit", "Priv_Debit", "BranchID",
                                     "UserID_Add", "UserBranch_Add", "UserMacAddress_Add", "UserDate_Add",
                                     "UserID_Update", "UserBranch_Update", "UserMacAddress_Update", "UserDate_Update" };
        
        foreach (var colName in hiddenColumns)
        {
            if (_costCentersGrid.Columns.Contains(colName))
                _costCentersGrid.Columns[colName].Visible = false;
        }
        
        // Rename columns
        if (_costCentersGrid.Columns.Contains("CostCentersNo")) _costCentersGrid.Columns["CostCentersNo"].HeaderText = "رقم مركز التكلفة"; // "رقم مركز التكلفة"
        if (_costCentersGrid.Columns.Contains("CostCentersName")) _costCentersGrid.Columns["CostCentersName"].HeaderText = "اسم مركز التكلفة"; // "اسم مركز التكلفة"
    }

    private void ApplyFilter()
    {
        if (_accountsMode && _accountsData != null)
        {
            var filter = string.Empty;
            var searchTerm = _searchText.Text.Trim();
            
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filter = $"Convert([Account_Name], 'System.String') LIKE '%{searchTerm}%' OR Convert([Account_No], 'System.String') LIKE '%{searchTerm}%' OR Convert([E_Account_Name], 'System.String') LIKE '%{searchTerm}%' ";
            }
            
            _accountsData.DefaultView.RowFilter = filter;
        }
        else if (!_accountsMode && _costCentersData != null)
        {
            var filter = string.Empty;
            var searchTerm = _searchText.Text.Trim();
            
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filter = $"Convert([CostCentersName], 'System.String') LIKE '%{searchTerm}%' OR Convert([CostCentersNo], 'System.String') LIKE '%{searchTerm}%' ";
            }
            
            _costCentersData.DefaultView.RowFilter = filter;
        }
    }

    private void LoadAccountDetails()
    {
        if (_accountsMode)
        {
            if (_accountsGrid.CurrentRow?.DataBoundItem is not DataRowView row)
                return;
            
            _selectedAccountId = Convert.ToInt32(row.Row["ID"]);
            _accountNoText.Text = Convert.ToString(row.Row["Account_No"]) ?? string.Empty;
            _accountNameText.Text = Convert.ToString(row.Row["Account_Name"]) ?? string.Empty;
            _englishNameText.Text = Convert.ToString(row.Row["E_Account_Name"]) ?? string.Empty;
            _mainAccountText.Text = Convert.ToString(row.Row["Main_Account_No"]) ?? string.Empty;
            _phoneText.Text = Convert.ToString(row.Row["PhoneA"]) ?? string.Empty;
            _addressText.Text = Convert.ToString(row.Row["AddressA"]) ?? string.Empty;
            _notesText.Text = Convert.ToString(row.Row["Note"]) ?? string.Empty;
            
            if (row.Row["Account_Type"] != DBNull.Value)
                _accountTypeCombo.SelectedIndex = Convert.ToInt32(row.Row["Account_Type"]) - 1;
            if (row.Row["Account_Nature"] != DBNull.Value)
                _accountNatureCombo.SelectedIndex = Convert.ToInt32(row.Row["Account_Nature"]) - 1;
        }
        else
        {
            if (_costCentersGrid.CurrentRow?.DataBoundItem is not DataRowView row)
                return;
            
            _selectedAccountId = Convert.ToInt32(row.Row["SN"]);
            _accountNoText.Text = Convert.ToString(row.Row["CostCentersNo"]) ?? string.Empty;
            _accountNameText.Text = Convert.ToString(row.Row["CostCentersName"]) ?? string.Empty;
            _notesText.Text = Convert.ToString(row.Row["Note"]) ?? string.Empty;
        }
    }

    private void ClearDetails()
    {
        _selectedAccountId = null;
        _accountNoText.Clear();
        _accountNameText.Clear();
        _englishNameText.Clear();
        _mainAccountText.Clear();
        _phoneText.Clear();
        _addressText.Clear();
        _notesText.Clear();
        _accountTypeCombo.SelectedIndex = -1;
        _accountNatureCombo.SelectedIndex = -1;
    }

    private void SaveAccount()
    {
        if (!_access.AllowSave) return;
        
        MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "حفظ الحسابات متاح في النظام الكامل"
    }

    private void EditAccount()
    {
        if (!_access.AllowEdit) return;
        
        MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "تعديل الحسابات متاح في النظام الكامل"
    }

    private void DeleteAccount()
    {
        if (!_access.AllowDelete) return;
        
        MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "حذف الحسابات متاح في النظام الكامل"
    }

    private void ExportAccounts()
    {
        if (!_access.AllowExport) return;
        
        var data = _accountsMode ? _accountsData : _costCentersData;
        if (data == null) return;
        
        using var dialog = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = _accountsMode ? "Accounts.csv" : "CostCenters.csv", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        
        var sb = new System.Text.StringBuilder();
        
        if (_accountsMode)
        {
            // Header for accounts
            var headers = new[] { "\u0001", "\u0001", "\u0001", "\u0001", "\u0001" }; // "رقم الحساب", "اسم الحساب", "الاسم الانجليزي", "نوع الحساب", "طبيعة الحساب"
            sb.AppendLine(string.Join(",", headers));
            
            foreach (DataRowView view in data.DefaultView)
            {
                var values = new[]
                {
                    view.Row["Account_No"]?.ToString() ?? string.Empty,
                    view.Row["Account_Name"]?.ToString() ?? string.Empty,
                    view.Row["E_Account_Name"]?.ToString() ?? string.Empty,
                    view.Row["Account_Type"]?.ToString() ?? string.Empty,
                    view.Row["Account_Nature"]?.ToString() ?? string.Empty
                };
                sb.AppendLine(string.Join(",", values.Select(v => EscapeCsv(v))));
            }
        }
        else
        {
            // Header for cost centers
            var headers = new[] { "\u0001", "\u0001" }; // "رقم مركز التكلفة", "اسم مركز التكلفة"
            sb.AppendLine(string.Join(",", headers));
            
            foreach (DataRowView view in data.DefaultView)
            {
                var values = new[]
                {
                    view.Row["CostCentersNo"]?.ToString() ?? string.Empty,
                    view.Row["CostCentersName"]?.ToString() ?? string.Empty
                };
                sb.AppendLine(string.Join(",", values.Select(v => EscapeCsv(v))));
            }
        }
        
        File.WriteAllText(dialog.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
        _status.Text = "تم التصدير إلى: ..." + dialog.FileName; // "تم التصدير إلى: ..."
    }

    private void ShowTrialBalance()
    {
        using var form = new TrialBalanceForm(_session, _access, _service);
        form.StartPosition = FormStartPosition.CenterParent;
        form.ShowDialog(this);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}

/// <summary>
/// Form for displaying Trial Balance
/// </summary>
internal sealed class TrialBalanceForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly AccountingService _service;
    
    private readonly DataGridView _grid = new();
    private readonly DateTimePicker _fromDate = new();
    private readonly DateTimePicker _toDate = new();
    private readonly Label _status = new();
    
    public TrialBalanceForm(AppSession session, ScreenAccess access, AccountingService service)
    {
        _session = session;
        _access = access;
        _service = service;
        
        Text = "ميزان المراجعة"; // "ميزان المراجعة"
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label
        {
            Text = "ميزان المراجعة", // "ميزان المراجعة"
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        
        var dateLayout = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 35, ColumnCount = 5, RowCount = 1 };
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        
        var fromLabel = new Label { Text = "من:", TextAlign = ContentAlignment.MiddleRight }; // "من:"
        _fromDate.Format = DateTimePickerFormat.Short;
        _fromDate.Value = DateTime.Today.AddDays(-30);
        _fromDate.RightToLeft = RightToLeft.Yes;
        
        var toLabel = new Label { Text = "إلى:", TextAlign = ContentAlignment.MiddleRight }; // "إلى:"
        _toDate.Format = DateTimePickerFormat.Short;
        _toDate.Value = DateTime.Today;
        _toDate.RightToLeft = RightToLeft.Yes;
        
        var refreshBtn = new Button { Text = "تحديث", Width = 80, Height = 30 }; // "تحديث"
        refreshBtn.Click += async (_, _) => await LoadDataAsync();
        
        dateLayout.Controls.Add(fromLabel, 0, 0);
        dateLayout.Controls.Add(_fromDate, 1, 0);
        dateLayout.Controls.Add(toLabel, 2, 0);
        dateLayout.Controls.Add(_toDate, 3, 0);
        dateLayout.Controls.Add(refreshBtn, 4, 0);
        
        header.Controls.Add(title);
        header.Controls.Add(dateLayout);

        // Grid
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _grid.RightToLeft = RightToLeft.Yes;
        _grid.RowHeadersVisible = false;

        // Status
        _status.Dock = DockStyle.Bottom;
        _status.Height = 32;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Padding = new Padding(8);
        _status.BorderStyle = BorderStyle.FixedSingle;
        _status.BackColor = Color.FromArgb(220, 230, 241);

        Controls.Add(header);
        Controls.Add(_grid);
        Controls.Add(_status);

        Shown += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            UseWaitCursor = true;
            var data = await _service.GetTrialBalanceAsync(_fromDate.Value, _toDate.Value, _session.BranchId);
            _grid.DataSource = data;
            FormatGrid();
            _status.Text = $"عدد الحسابات: ..."; // "عدد الحسابات: ..."
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ: " + ex.GetBaseException().Message; // "خطأ: "
        }
        finally { UseWaitCursor = false; }
    }

    private void FormatGrid()
    {
        if (_grid.Columns.Count == 0) return;
        
        // Rename columns
        if (_grid.Columns.Contains("Account_No")) _grid.Columns["Account_No"].HeaderText = "رقم الحساب"; // "رقم الحساب"
        if (_grid.Columns.Contains("Account_Name")) _grid.Columns["Account_Name"].HeaderText = "اسم الحساب"; // "اسم الحساب"
        if (_grid.Columns.Contains("TotalDebit")) _grid.Columns["TotalDebit"].HeaderText = "مدين"; // "مدين"
        if (_grid.Columns.Contains("TotalCredit")) _grid.Columns["TotalCredit"].HeaderText = "دائن"; // "دائن"
        if (_grid.Columns.Contains("Balance")) _grid.Columns["Balance"].HeaderText = "الرصيد"; // "الرصيد"
    }
}
