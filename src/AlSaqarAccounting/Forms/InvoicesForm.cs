using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Real ERP Form for Invoice management.
/// Handles both sales and purchase invoices with full CRUD operations.
/// </summary>
public sealed class InvoicesForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly InvoiceService _invoiceService;
    private readonly CustomerService _customerService;
    private readonly SupplierService _supplierService;
    private readonly ItemsService _itemsService;
    
    private readonly DataGridView _invoicesGrid = new();
    private readonly DataGridView _detailsGrid = new();
    private readonly TextBox _searchText = new();
    private readonly TextBox _customerSupplierText = new();
    private readonly TextBox _invoiceNumberText = new();
    private readonly DateTimePicker _dateFrom = new();
    private readonly DateTimePicker _dateTo = new();
    private readonly ComboBox _invoiceTypeCombo = new();
    private readonly Label _status = new();
    
    private DataTable? _invoicesData;
    private DataTable? _detailsData;
    private bool _salesMode = true;
    private int? _selectedInvoiceId;

    public InvoicesForm(
        AppSession session,
        ScreenAccess access,
        InvoiceService invoiceService,
        CustomerService customerService,
        SupplierService supplierService,
        ItemsService itemsService)
    {
        _session = session;
        _access = access;
        _invoiceService = invoiceService;
        _customerService = customerService;
        _supplierService = supplierService;
        _itemsService = itemsService;
        
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = "\u0001\u0001 - \u0001"; // "النظام المحاسبي - إدارة الفواتير"
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
            Text = "\u0001", // "إدارة الفواتير"
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"\u0001: {_session.UserName} | \u0001: {_session.BranchId?.ToString() ?? "-"}", // "المستخدم: ... | الفرع: ..."
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(title);
        header.Controls.Add(info);

        // Filter Panel
        var filterPanel = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        
        var filterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1 };
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        
        var searchLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "بحث:"
        _searchText.Dock = DockStyle.Fill;
        _searchText.RightToLeft = RightToLeft.Yes;
        _searchText.PlaceholderText = "\u0001"; // "ابحث..."
        
        var typeLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "نوع الفاتورة:"
        _invoiceTypeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _invoiceTypeCombo.Items.AddRange(new object[] { "\u0001", "\u0001" }); // "مبيعات", "مشتريات"
        _invoiceTypeCombo.SelectedIndex = 0;
        _invoiceTypeCombo.SelectedIndexChanged += (_, _) => { _salesMode = _invoiceTypeCombo.SelectedIndex == 0; LoadInvoices(); };
        
        var customerLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "العميل/المورد:"
        _customerSupplierText.Dock = DockStyle.Fill;
        _customerSupplierText.RightToLeft = RightToLeft.Yes;
        
        var numberLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "رقم الفاتورة:"
        _invoiceNumberText.Dock = DockStyle.Fill;
        _invoiceNumberText.RightToLeft = RightToLeft.Yes;
        
        var fromLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "من:"
        _dateFrom.Format = DateTimePickerFormat.Short;
        _dateFrom.Value = DateTime.Today.AddDays(-30);
        _dateFrom.RightToLeft = RightToLeft.Yes;
        
        var toLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "إلى:"
        _dateTo.Format = DateTimePickerFormat.Short;
        _dateTo.Value = DateTime.Today;
        _dateTo.RightToLeft = RightToLeft.Yes;
        
        var refreshBtn = new Button { Text = "\u0001", Width = 80, Height = 30 }; // "تحديث"
        refreshBtn.Click += (_, _) => LoadInvoices();
        
        filterLayout.Controls.Add(searchLabel, 0, 0);
        filterLayout.Controls.Add(_searchText, 1, 0);
        filterLayout.Controls.Add(typeLabel, 2, 0);
        filterLayout.Controls.Add(_invoiceTypeCombo, 3, 0);
        filterLayout.Controls.Add(customerLabel, 4, 0);
        filterLayout.Controls.Add(_customerSupplierText, 5, 0);
        filterLayout.Controls.Add(fromLabel, 6, 0);
        filterLayout.Controls.Add(_dateFrom, 7, 0);
        
        var dateLayout = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 4, RowCount = 1, Height = 35 };
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dateLayout.Controls.Add(toLabel, 0, 0);
        dateLayout.Controls.Add(_dateTo, 1, 0);
        dateLayout.Controls.Add(refreshBtn, 2, 0);
        
        filterPanel.Controls.Add(filterLayout);
        filterPanel.Controls.Add(dateLayout);

        // Split Container
        var splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 400 };
        
        // Top Panel - Invoices List
        var invoicesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var invoicesTitle = new Label { Text = "\u0001", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "قائمة الفواتير"
        
        _invoicesGrid.Dock = DockStyle.Fill;
        _invoicesGrid.ReadOnly = true;
        _invoicesGrid.AllowUserToAddRows = false;
        _invoicesGrid.AllowUserToDeleteRows = false;
        _invoicesGrid.AutoGenerateColumns = true;
        _invoicesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _invoicesGrid.RightToLeft = RightToLeft.Yes;
        _invoicesGrid.RowHeadersVisible = false;
        _invoicesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _invoicesGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await ShowInvoiceDetailsAsync(); };
        
        invoicesPanel.Controls.Add(_invoicesGrid);
        invoicesPanel.Controls.Add(invoicesTitle);
        splitContainer.Panel1.Controls.Add(invoicesPanel);

        // Bottom Panel - Invoice Details
        var detailsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var detailsTitle = new Label { Text = "\u0001", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "تفاصيل الفاتورة"
        
        _detailsGrid.Dock = DockStyle.Fill;
        _detailsGrid.ReadOnly = true;
        _detailsGrid.AllowUserToAddRows = false;
        _detailsGrid.AllowUserToDeleteRows = false;
        _detailsGrid.AutoGenerateColumns = true;
        _detailsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _detailsGrid.RightToLeft = RightToLeft.Yes;
        _detailsGrid.RowHeadersVisible = false;
        
        detailsPanel.Controls.Add(_detailsGrid);
        detailsPanel.Controls.Add(detailsTitle);
        splitContainer.Panel2.Controls.Add(detailsPanel);

        // Toolbar Panel
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), WrapContents = false, BackColor = Color.FromArgb(240, 248, 255) };
        
        AddToolbarButton(toolbar, "\u0001", _access.AllowSave, CreateNewInvoice); // "جديد"
        AddToolbarButton(toolbar, "\u0001", _access.AllowEdit, EditInvoice); // "تعديل"
        AddToolbarButton(toolbar, "\u0001", _access.AllowDelete, DeleteInvoice); // "حذف"
        AddToolbarButton(toolbar, "\u0001", _access.AllowPrint, PrintInvoice); // "طباعة"
        AddToolbarButton(toolbar, "\u0001", _access.AllowExport, ExportInvoices); // "تصدير"

        // Status Bar
        _status.Dock = DockStyle.Bottom;
        _status.Height = 32;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Padding = new Padding(8);
        _status.BorderStyle = BorderStyle.FixedSingle;
        _status.BackColor = Color.FromArgb(220, 230, 241);

        Controls.Add(header);
        Controls.Add(filterPanel);
        Controls.Add(splitContainer);
        Controls.Add(toolbar);
        Controls.Add(_status);

        // Event handlers
        _searchText.TextChanged += (_, _) => LoadInvoices();
        _customerSupplierText.TextChanged += (_, _) => LoadInvoices();
        _dateFrom.ValueChanged += (_, _) => LoadInvoices();
        _dateTo.ValueChanged += (_, _) => LoadInvoices();
        
        Shown += async (_, _) => await LoadInitialDataAsync();
    }

    private static void AddToolbarButton(FlowLayoutPanel panel, string text, bool enabled, Action action)
    {
        var button = new Button { Text = text, Width = 100, Height = 36, Enabled = enabled, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            UseWaitCursor = true;
            await LoadInvoices();
            _status.Text = "\u0001"; // "النظام جاهز"
        }
        catch (Exception ex)
        {
            _status.Text = "\u0001: " + ex.GetBaseException().Message; // "خطأ: "
        }
        finally { UseWaitCursor = false; }
    }

    private async Task LoadInvoices()
    {
        try
        {
            UseWaitCursor = true;
            
            if (_salesMode)
            {
                _invoicesData = await _invoiceService.GetInvoicesAsync(
                    true, 
                    _session.BranchId,
                    _dateFrom.Value,
                    _dateTo.Value);
            }
            else
            {
                _invoicesData = await _invoiceService.GetPurchaseInvoicesAsync(
                    _session.BranchId,
                    _dateFrom.Value,
                    _dateTo.Value);
            }
            
            ApplyFilters();
            _invoicesGrid.DataSource = _invoicesData;
            FormatInvoicesGrid();
            _status.Text = $"\u0001: {_invoicesData.Rows.Count:N0}"; // "عدد الفواتير: ..."
        }
        catch (Exception ex)
        {
            _status.Text = "\u0001: " + ex.GetBaseException().Message; // "خطأ في تحميل الفواتير: "
        }
        finally { UseWaitCursor = false; }
    }

    private void ApplyFilters()
    {
        if (_invoicesData == null) return;
        
        var filter = string.Empty;
        var searchTerm = _searchText.Text.Trim();
        var customerTerm = _customerSupplierText.Text.Trim();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filter = $"Convert([NoteNum], 'System.String') LIKE '%{searchTerm}%' OR Convert([SupplierName], 'System.String') LIKE '%{searchTerm}%' OR Convert([Note], 'System.String') LIKE '%{searchTerm}%' ";
        }
        
        if (!string.IsNullOrWhiteSpace(customerTerm))
        {
            if (!string.IsNullOrWhiteSpace(filter))
                filter += "AND ";
            filter += $"Convert([SupplierName], 'System.String') LIKE '%{customerTerm}%' ";
        }
        
        _invoicesData.DefaultView.RowFilter = filter;
    }

    private void FormatInvoicesGrid()
    {
        if (_invoicesGrid.Columns.Count == 0) return;
        
        // Hide unnecessary columns
        var hiddenColumns = new[] { "ID", "PurBranchID", "SupplierID", "SupplierVatNum", "Order_Paymant_Type", 
                                     "CostCentersID", "UserID_Add", "UserBranch_Add", "UserMacAddress_Add", 
                                     "UserDate_Add", "UserID_Update", "UserBranch_Update", "UserMacAddress_Update",
                                     "UserDate_Update", "ProjectId", "YearId", "Acc_Cash", "Acc_Bank",
                                     "SalesMan", "Charge", "Cash", "Bank", "RoomNum", "TableNum",
                                     "RestBraID", "RestBraISClosed", "BounceID", "Res_Delivery_ID",
                                     "BuildingNum", "Street", "District", "City", "Country", "PostalCode",
                                     "AdditionalNum", "CommercialRecord" };
        
        foreach (var colName in hiddenColumns)
        {
            if (_invoicesGrid.Columns.Contains(colName))
                _invoicesGrid.Columns[colName].Visible = false;
        }
        
        // Rename columns
        if (_invoicesGrid.Columns.Contains("NoteNum")) _invoicesGrid.Columns["NoteNum"].HeaderText = "\u0001"; // "رقم الفاتورة"
        if (_invoicesGrid.Columns.Contains("SupplierName")) _invoicesGrid.Columns["SupplierName"].HeaderText = _salesMode ? "\u0001" : "\u0001"; // "العميل" or "المورد"
        if (_invoicesGrid.Columns.Contains("Purchases_Date")) _invoicesGrid.Columns["Purchases_Date"].HeaderText = "\u0001"; // "التاريخ"
        if (_invoicesGrid.Columns.Contains("TotalPrices")) _invoicesGrid.Columns["TotalPrices"].HeaderText = "\u0001"; // "الإجمالي"
        if (_invoicesGrid.Columns.Contains("Tax")) _invoicesGrid.Columns["Tax"].HeaderText = "\u0001"; // "الضريبة"
        if (_invoicesGrid.Columns.Contains("Net")) _invoicesGrid.Columns["Net"].HeaderText = "\u0001"; // "الصافي"
    }

    private async Task ShowInvoiceDetailsAsync()
    {
        if (_invoicesGrid.CurrentRow?.DataBoundItem is not DataRowView row)
            return;
        
        _selectedInvoiceId = Convert.ToInt32(row.Row["ID"]);
        
        try
        {
            if (_salesMode)
            {
                _detailsData = await _invoiceService.GetInvoiceDetailsAsync(_selectedInvoiceId.Value);
            }
            else
            {
                // For purchase invoices
                _detailsData = await _invoiceService.GetPurchaseInvoiceDetailsAsync(_selectedInvoiceId.Value);
            }
            
            _detailsGrid.DataSource = _detailsData;
            FormatDetailsGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ"
        }
    }

    private void FormatDetailsGrid()
    {
        if (_detailsGrid.Columns.Count == 0) return;
        
        // Hide unnecessary columns
        var hiddenColumns = new[] { "SN", "Purchese_ID", "R_ItmSN", "R_RowId", "R_ADDId", "ItemIDADD", 
                                     "IsWaiting", "IsPrint", "IsPrintCook", "UnitNumber", "Weight", 
                                     "Height", "DetailsData", "width", "Long", "Bounce",
                                     "Amount_Discount", "Per_Discount", "TafqitUnitId", "TafqitQunitity",
                                     "TobaccoTax", "TobaccoTaxDis", "TotalWithTaxTobacco", "PriceInstall",
                                     "PriceInstallDise" };
        
        foreach (var colName in hiddenColumns)
        {
            if (_detailsGrid.Columns.Contains(colName))
                _detailsGrid.Columns[colName].Visible = false;
        }
        
        // Rename columns
        if (_detailsGrid.Columns.Contains("ItemID")) _detailsGrid.Columns["ItemID"].HeaderText = "\u0001"; // "كود المنتج"
        if (_detailsGrid.Columns.Contains("Quantity")) _detailsGrid.Columns["Quantity"].HeaderText = "\u0001"; // "الكمية"
        if (_detailsGrid.Columns.Contains("UnitPrice")) _detailsGrid.Columns["UnitPrice"].HeaderText = "\u0001"; // "سعر الوحدة"
        if (_detailsGrid.Columns.Contains("TotalPrice")) _detailsGrid.Columns["TotalPrice"].HeaderText = "\u0001"; // "الإجمالي"
        if (_detailsGrid.Columns.Contains("VAT")) _detailsGrid.Columns["VAT"].HeaderText = "\u0001"; // "الضريبة"
    }

    private void CreateNewInvoice()
    {
        if (!_access.AllowSave) return;
        
        using var form = new InvoiceEditForm(
            _session,
            _access,
            _invoiceService,
            _customerService,
            _supplierService,
            _itemsService,
            _salesMode);
        
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadInvoices();
        }
    }

    private void EditInvoice()
    {
        if (!_access.AllowEdit || !_selectedInvoiceId.HasValue)
        {
            MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "يرجى اختيار فاتورة"
            return;
        }
        
        using var form = new InvoiceEditForm(
            _session,
            _access,
            _invoiceService,
            _customerService,
            _supplierService,
            _itemsService,
            _salesMode,
            _selectedInvoiceId.Value);
        
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadInvoices();
        }
    }

    private async void DeleteInvoice()
    {
        if (!_access.AllowDelete || !_selectedInvoiceId.HasValue)
        {
            MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "يرجى اختيار فاتورة"
            return;
        }
        
        if (MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) // "هل تريد حذف الفاتورة؟"
            return;
        
        try
        {
            UseWaitCursor = true;
            await _invoiceService.DeleteInvoiceAsync(_selectedInvoiceId.Value);
            await LoadInvoices();
            _status.Text = "\u0001"; // "تم حذف الفاتورة"
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ"
        }
        finally { UseWaitCursor = false; }
    }

    private void PrintInvoice()
    {
        if (!_selectedInvoiceId.HasValue)
        {
            MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "يرجى اختيار فاتورة"
            return;
        }
        
        MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "طباعة الفواتير قيد التطوير"
    }

    private void ExportInvoices()
    {
        if (!_access.AllowExport || _invoicesData == null) return;
        
        using var dialog = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = "Invoices.csv", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        
        var sb = new System.Text.StringBuilder();
        
        // Header
        var headers = new[] { "\u0001", "\u0001", "\u0001", "\u0001", "\u0001", "\u0001" }; // "رقم", "التاريخ", "العميل/المورد", "الإجمالي", "الضريبة", "الصافي"
        sb.AppendLine(string.Join(",", headers));
        
        // Data
        foreach (DataRowView view in _invoicesData.DefaultView)
        {
            var values = new[]
            {
                view.Row["NoteNum"]?.ToString() ?? string.Empty,
                view.Row["Purchases_Date"]?.ToString() ?? string.Empty,
                view.Row["SupplierName"]?.ToString() ?? string.Empty,
                view.Row["TotalPrices"]?.ToString() ?? string.Empty,
                view.Row["Tax"]?.ToString() ?? string.Empty,
                view.Row["Net"]?.ToString() ?? string.Empty
            };
            sb.AppendLine(string.Join(",", values.Select(v => EscapeCsv(v))));
        }
        
        File.WriteAllText(dialog.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
        _status.Text = "\u0001: " + dialog.FileName; // "تم التصدير إلى: ..."
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}

/// <summary>
/// Form for creating/editing invoices
/// </summary>
internal sealed class InvoiceEditForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly InvoiceService _invoiceService;
    private readonly CustomerService _customerService;
    private readonly SupplierService _supplierService;
    private readonly ItemsService _itemsService;
    private readonly bool _salesMode;
    private readonly int? _invoiceId;
    
    private readonly DataGridView _itemsGrid = new();
    private readonly TextBox _invoiceNumberText = new();
    private readonly DateTimePicker _datePicker = new();
    private readonly TextBox _customerSupplierText = new();
    private readonly TextBox _notesText = new();
    private readonly TextBox _totalText = new();
    private readonly TextBox _taxText = new();
    private readonly TextBox _netText = new();
    private readonly Button _saveBtn = new();
    private readonly Button _cancelBtn = new();
    
    private DataTable? _itemsData;
    private DataTable? _customersData;
    private DataTable? _suppliersData;
    private DataTable? _invoiceDetails;
    
    public InvoiceEditForm(
        AppSession session,
        ScreenAccess access,
        InvoiceService invoiceService,
        CustomerService customerService,
        SupplierService supplierService,
        ItemsService itemsService,
        bool salesMode,
        int? invoiceId = null)
    {
        _session = session;
        _access = access;
        _invoiceService = invoiceService;
        _customerService = customerService;
        _supplierService = supplierService;
        _itemsService = itemsService;
        _salesMode = salesMode;
        _invoiceId = invoiceId;
        
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = _invoiceId.HasValue ? "\u0001" : "\u0001"; // "تعديل الفاتورة" or "فاتورة جديدة"
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label
        {
            Text = _invoiceId.HasValue ? "\u0001" : "\u0001", // "تعديل الفاتورة" or "فاتورة جديدة"
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(title);

        // Form Panel
        var formPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 180, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
        formPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        formPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        formPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        formPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(formPanel, "\u0001", _invoiceNumberText, 0, 0); // "رقم الفاتورة"
        AddField(formPanel, "\u0001", _datePicker, 1, 0); // "التاريخ"
        formPanel.SetColumnSpan(_datePicker, 2);
        AddField(formPanel, _salesMode ? "\u0001" : "\u0001", _customerSupplierText, 0, 1); // "العميل" or "المورد"
        formPanel.SetColumnSpan(_customerSupplierText, 3);
        AddField(formPanel, "\u0001", _notesText, 0, 2, true); // "ملاحظات"
        formPanel.SetColumnSpan(_notesText, 3);

        // Items Grid
        _itemsGrid.Dock = DockStyle.Fill;
        _itemsGrid.ReadOnly = true;
        _itemsGrid.AllowUserToAddRows = false;
        _itemsGrid.AllowUserToDeleteRows = false;
        _itemsGrid.AutoGenerateColumns = true;
        _itemsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _itemsGrid.RightToLeft = RightToLeft.Yes;
        _itemsGrid.RowHeadersVisible = false;
        _itemsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        // Totals Panel
        var totalsPanel = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        
        var totalsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        totalsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        totalsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        totalsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        totalsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        
        AddField(totalsLayout, "\u0001", _totalText, 0, 0); // "الإجمالي"
        AddField(totalsLayout, "\u0001", _taxText, 1, 0); // "الضريبة"
        AddField(totalsLayout, "\u0001", _netText, 2, 0); // "الصافي"
        
        totalsPanel.Controls.Add(totalsLayout);

        // Buttons Panel
        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8), WrapContents = false };
        
        _saveBtn.Text = "\u0001"; // "حفظ"
        _saveBtn.Width = 100;
        _saveBtn.Height = 36;
        _saveBtn.DialogResult = DialogResult.OK;
        _saveBtn.Click += async (_, _) => await SaveInvoiceAsync();
        
        _cancelBtn.Text = "\u0001"; // "إلغاء"
        _cancelBtn.Width = 100;
        _cancelBtn.Height = 36;
        _cancelBtn.DialogResult = DialogResult.Cancel;
        
        buttonsPanel.Controls.Add(_saveBtn);
        buttonsPanel.Controls.Add(_cancelBtn);

        Controls.Add(header);
        Controls.Add(formPanel);
        Controls.Add(_itemsGrid);
        Controls.Add(totalsPanel);
        Controls.Add(buttonsPanel);
        
        AcceptButton = _saveBtn;
        CancelButton = _cancelBtn;

        Shown += async (_, _) => await LoadDataAsync();
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
        box.Controls.Add(control);
        box.Controls.Add(text);
        panel.Controls.Add(box, column, row);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            UseWaitCursor = true;
            
            _itemsData = await _itemsService.ListAsync();
            _customersData = await _customerService.ListAsync(_session.BranchId);
            _suppliersData = await _supplierService.ListAsync(_session.BranchId);
            
            if (_invoiceId.HasValue)
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(_invoiceId.Value);
                if (invoice != null)
                {
                    _invoiceNumberText.Text = invoice.NoteNum ?? string.Empty;
                    _datePicker.Value = invoice.Purchases_Date ?? DateTime.Now;
                    _customerSupplierText.Text = invoice.SupplierName ?? string.Empty;
                    _notesText.Text = invoice.Note ?? string.Empty;
                    _totalText.Text = (invoice.TotalPrices ?? 0).ToString("N2");
                    _taxText.Text = (invoice.Tax ?? 0).ToString("N2");
                    _netText.Text = (invoice.Net ?? 0).ToString("N2");
                    
                    if (_salesMode)
                    {
                        _invoiceDetails = await _invoiceService.GetInvoiceDetailsAsync(_invoiceId.Value);
                    }
                    else
                    {
                        _invoiceDetails = await _invoiceService.GetPurchaseInvoiceDetailsAsync(_invoiceId.Value);
                    }
                    
                    _itemsGrid.DataSource = _invoiceDetails;
                }
            }
            else
            {
                // New invoice
                _invoiceNumberText.Text = GenerateInvoiceNumber();
                _datePicker.Value = DateTime.Now;
                _totalText.Text = "0.00";
                _taxText.Text = "0.00";
                _netText.Text = "0.00";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ"
        }
        finally { UseWaitCursor = false; }
    }

    private string GenerateInvoiceNumber()
    {
        return $"INV-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
    }

    private async Task SaveInvoiceAsync()
    {
        if (!_access.AllowSave)
        {
            DialogResult = DialogResult.Cancel;
            return;
        }
        
        try
        {
            UseWaitCursor = true;
            
            if (_invoiceId.HasValue)
            {
                // Update existing invoice
                var invoice = new Order_Orders
                {
                    ID = _invoiceId.Value,
                    NoteNum = _invoiceNumberText.Text.Trim(),
                    SupplierName = _customerSupplierText.Text.Trim(),
                    Purchases_Date = _datePicker.Value,
                    Note = _notesText.Text.Trim(),
                    TotalPrices = decimal.Parse(_totalText.Text),
                    Tax = decimal.Parse(_taxText.Text),
                    Net = decimal.Parse(_netText.Text)
                };
                
                await _invoiceService.UpdateInvoiceAsync(invoice, _session);
                MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "تم تحديث الفاتورة"
            }
            else
            {
                // Create new invoice
                var invoice = new Order_Orders
                {
                    NoteNum = _invoiceNumberText.Text.Trim(),
                    SupplierName = _customerSupplierText.Text.Trim(),
                    Purchases_Date = _datePicker.Value,
                    Note = _notesText.Text.Trim(),
                    TotalPrices = decimal.Parse(_totalText.Text),
                    Tax = decimal.Parse(_taxText.Text),
                    Net = decimal.Parse(_netText.Text),
                    OrderCashierType = false
                };
                
                // For demo, create with empty details
                var details = new List<Order_OrdersDetails>();
                
                var invoiceId = await _invoiceService.CreateSalesInvoiceAsync(invoice, details, _session);
                MessageBox.Show(this, $"\u0001 {invoiceId}", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "تم إنشاء الفاتورة رقم: ..."
            }
            
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ"
        }
        finally { UseWaitCursor = false; }
    }
}
