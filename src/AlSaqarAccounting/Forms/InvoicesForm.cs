using System.Data;
using System.Drawing.Printing;
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
    private readonly SalesService _salesService;
    private readonly StoresService _storesService;
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
        SalesService salesService,
        StoresService storesService,
        CustomerService customerService,
        SupplierService supplierService,
        ItemsService itemsService)
    {
        _session = session;
        _access = access;
        _invoiceService = invoiceService;
        _salesService = salesService;
        _storesService = storesService;
        _customerService = customerService;
        _supplierService = supplierService;
        _itemsService = itemsService;
        
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = "الصقر للمحاسبة - إدارة الفواتير";
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
            Text = "إدارة الفواتير",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString() ?? "-"}",
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
        
        var searchLabel = new Label { Text = "بحث:", TextAlign = ContentAlignment.MiddleRight };
        _searchText.Dock = DockStyle.Fill;
        _searchText.RightToLeft = RightToLeft.Yes;
        
        var typeLabel = new Label { Text = "نوع الفاتورة:", TextAlign = ContentAlignment.MiddleRight };
        _invoiceTypeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _invoiceTypeCombo.Items.AddRange(new object[] { "مبيعات", "مشتريات" });
        _invoiceTypeCombo.SelectedIndex = 0;
        _invoiceTypeCombo.SelectedIndexChanged += (_, _) => { _salesMode = _invoiceTypeCombo.SelectedIndex == 0; LoadInvoices(); };
        
        var customerLabel = new Label { Text = "العميل/المورد:", TextAlign = ContentAlignment.MiddleRight };
        _customerSupplierText.Dock = DockStyle.Fill;
        _customerSupplierText.RightToLeft = RightToLeft.Yes;
        
        var numberLabel = new Label { Text = "رقم الفاتورة:", TextAlign = ContentAlignment.MiddleRight };
        _invoiceNumberText.Dock = DockStyle.Fill;
        _invoiceNumberText.RightToLeft = RightToLeft.Yes;
        
        var fromLabel = new Label { Text = "من:", TextAlign = ContentAlignment.MiddleRight };
        _dateFrom.Format = DateTimePickerFormat.Short;
        _dateFrom.Value = DateTime.Today.AddDays(-30);
        _dateFrom.RightToLeft = RightToLeft.Yes;
        
        var toLabel = new Label { Text = "إلى:", TextAlign = ContentAlignment.MiddleRight };
        _dateTo.Format = DateTimePickerFormat.Short;
        _dateTo.Value = DateTime.Today;
        _dateTo.RightToLeft = RightToLeft.Yes;
        
        var refreshBtn = new Button { Text = "تحديث", Width = 80, Height = 30 };
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
        var invoicesTitle = new Label { Text = "قائمة الفواتير", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight };
        
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
        var detailsTitle = new Label { Text = "تفاصيل الفاتورة", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight };
        
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
        
        AddToolbarButton(toolbar, "جديد", _access.AllowSave, CreateNewInvoice);
        AddToolbarButton(toolbar, "تعديل", _access.AllowEdit && !_salesMode, EditInvoice);
        AddToolbarButton(toolbar, "حذف", _access.AllowDelete, DeleteInvoice);
        AddToolbarButton(toolbar, "طباعة", _access.AllowPrint, PrintInvoice);
        AddToolbarButton(toolbar, "تصدير", _access.AllowExport, ExportInvoices);

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
            _status.Text = "النظام جاهز";
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ: " + ex.GetBaseException().Message;
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
            _status.Text = $"عدد الفواتير: {_invoicesData.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ في تحميل الفواتير: " + ex.GetBaseException().Message;
        }
        finally { UseWaitCursor = false; }
    }

    private void ApplyFilters()
    {
        if (_invoicesData == null) return;
        
        var clauses = new System.Collections.Generic.List<string>();
        var searchTerm = EscapeRowFilterValue(_searchText.Text.Trim());
        var customerTerm = EscapeRowFilterValue(_customerSupplierText.Text.Trim());

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            clauses.Add($"(Convert([NoteNum], 'System.String') LIKE '%{searchTerm}%' OR Convert([SupplierName], 'System.String') LIKE '%{searchTerm}%' OR Convert([Note], 'System.String') LIKE '%{searchTerm}%')");
        }

        if (!string.IsNullOrWhiteSpace(customerTerm))
        {
            clauses.Add($"Convert([SupplierName], 'System.String') LIKE '%{customerTerm}%'");
        }

        var filter = string.Join(" AND ", clauses);
        
        try
        {
            _invoicesData.DefaultView.RowFilter = filter;
        }
        catch
        {
            // Never allow a malformed user search expression to break the screen.
            _invoicesData.DefaultView.RowFilter = string.Empty;
        }
    }

    private static string EscapeRowFilterValue(string value)
    {
        return value
            .Replace("'", "''")
            .Replace("[", "[[]")
            .Replace("*", "[*]")
            .Replace("%", "[%]");
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
        if (_invoicesGrid.Columns.Contains("NoteNum")) _invoicesGrid.Columns["NoteNum"].HeaderText = "رقم الفاتورة";
        if (_invoicesGrid.Columns.Contains("SupplierName")) _invoicesGrid.Columns["SupplierName"].HeaderText = _salesMode ? "العميل" : "المورد";
        if (_invoicesGrid.Columns.Contains("Purchases_Date")) _invoicesGrid.Columns["Purchases_Date"].HeaderText = "التاريخ";
        if (_invoicesGrid.Columns.Contains("TotalPrices")) _invoicesGrid.Columns["TotalPrices"].HeaderText = "الإجمالي";
        if (_invoicesGrid.Columns.Contains("Tax")) _invoicesGrid.Columns["Tax"].HeaderText = "الضريبة";
        if (_invoicesGrid.Columns.Contains("Net")) _invoicesGrid.Columns["Net"].HeaderText = "الصافي";
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
                _detailsData = await _invoiceService.GetInvoiceDetailsAsync(_selectedInvoiceId.Value, _session);
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
            MessageBox.Show(this, ex.GetBaseException().Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        if (_detailsGrid.Columns.Contains("ItemID")) _detailsGrid.Columns["ItemID"].HeaderText = "كود المنتج";
        if (_detailsGrid.Columns.Contains("Quantity")) _detailsGrid.Columns["Quantity"].HeaderText = "الكمية";
        if (_detailsGrid.Columns.Contains("UnitPrice")) _detailsGrid.Columns["UnitPrice"].HeaderText = "سعر الوحدة";
        if (_detailsGrid.Columns.Contains("TotalPrice")) _detailsGrid.Columns["TotalPrice"].HeaderText = "الإجمالي";
        if (_detailsGrid.Columns.Contains("VAT")) _detailsGrid.Columns["VAT"].HeaderText = "الضريبة";
    }

    private async void CreateNewInvoice()
    {
        if (!_access.AllowSave)
            return;

        if (_salesMode)
        {
            using var salesForm = new SalesEntryForm(_session, _access, _salesService, _storesService);
            salesForm.ShowDialog(this);
            if (salesForm.Saved)
                await LoadInvoices();
            return;
        }

        using var purchaseForm = new InvoiceEditForm(
            _session,
            _access,
            _invoiceService,
            _customerService,
            _supplierService,
            _itemsService,
            false);

        if (purchaseForm.ShowDialog(this) == DialogResult.OK)
            await LoadInvoices();
    }

    private void EditInvoice()
    {
        if (_salesMode)
        {
            MessageBox.Show(this, "تعديل تفاصيل فاتورة المبيعات يتم من شاشة المبيعات التشغيلية.",
                "الفواتير", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_access.AllowEdit || !_selectedInvoiceId.HasValue)
        {
            MessageBox.Show(this, "يرجى اختيار فاتورة.", "الفواتير", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show(this, "يرجى اختيار فاتورة.", "الفواتير", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        
        if (MessageBox.Show(this, "هل تريد حذف الفاتورة؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) // "هل تريد حذف الفاتورة؟"
            return;
        
        try
        {
            UseWaitCursor = true;
            if (_salesMode)
                await _salesService.DeleteAsync(_selectedInvoiceId.Value, _session.BranchId);
            else
                await _invoiceService.DeleteInvoiceAsync(_selectedInvoiceId.Value, _session, _access.Id);
            await LoadInvoices();
            _status.Text = "تم حذف الفاتورة";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private async void PrintInvoice()
    {
        if (!_access.AllowPrint)
            return;

        if (!_selectedInvoiceId.HasValue || _invoicesGrid.CurrentRow?.DataBoundItem is not DataRowView headerRow)
        {
            MessageBox.Show(this, "يرجى اختيار فاتورة.", "الفواتير", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;

            var invoiceId = _selectedInvoiceId.Value;
            var details = _salesMode
                ? await _invoiceService.GetInvoiceDetailsAsync(invoiceId)
                : await _invoiceService.GetPurchaseInvoiceDetailsAsync(invoiceId);

            var invoiceNumber = GetRowValue(headerRow.Row, "NoteNum");
            var partyName = GetRowValue(headerRow.Row, "SupplierName");
            var dateText = GetRowValue(headerRow.Row, "Purchases_Date");
            var total = GetDecimalValue(headerRow.Row, "TotalPrices");
            var tax = GetDecimalValue(headerRow.Row, "Tax");
            var net = GetDecimalValue(headerRow.Row, "Net");

            using var printDoc = new PrintDocument();
            printDoc.DocumentName = $"{(_salesMode ? "فاتورة مبيعات" : "فاتورة مشتريات")} {invoiceNumber}".Trim();

            printDoc.PrintPage += (_, e) =>
            {
                using var titleFont = new Font("Tahoma", 16, FontStyle.Bold);
                using var headerFont = new Font("Tahoma", 11, FontStyle.Bold);
                using var bodyFont = new Font("Tahoma", 10, FontStyle.Regular);
                using var totalFont = new Font("Tahoma", 12, FontStyle.Bold);

                float y = e.MarginBounds.Top;
                float right = e.MarginBounds.Right;

                void DrawRight(string text, Font font)
                {
                    var size = e.Graphics.MeasureString(text, font);
                    e.Graphics.DrawString(text, font, Brushes.Black, right - size.Width, y);
                    y += size.Height + 6;
                }

                DrawRight("شركة الصقر للمحاسبة", titleFont);
                DrawRight(_salesMode ? "فاتورة مبيعات" : "فاتورة مشتريات", headerFont);
                DrawRight($"رقم الفاتورة: {invoiceNumber}", bodyFont);
                DrawRight($"التاريخ: {dateText}", bodyFont);
                DrawRight($"{(_salesMode ? "العميل" : "المورد")}: {partyName}", bodyFont);
                y += 8;

                e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, y, e.MarginBounds.Right, y);
                y += 10;

                DrawRight("الأصناف", headerFont);
                foreach (DataRow row in details.Rows)
                {
                    var itemId = GetRowValue(row, "ItemID");
                    var quantity = GetDecimalValue(row, "Quantity");
                    var unitPrice = GetDecimalValue(row, "UnitPrice");
                    var lineTotal = GetDecimalValue(row, "TotalPrice");
                    DrawRight($"الصنف {itemId} | الكمية: {quantity:N2} | السعر: {unitPrice:N2} | الإجمالي: {lineTotal:N2}", bodyFont);

                    if (y > e.MarginBounds.Bottom - 130)
                    {
                        e.HasMorePages = true;
                        return;
                    }
                }

                y += 10;
                e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, y, e.MarginBounds.Right, y);
                y += 12;

                DrawRight($"الإجمالي: {total:N2}", totalFont);
                DrawRight($"الضريبة: {tax:N2}", bodyFont);
                DrawRight($"الصافي: {net:N2}", totalFont);
                DrawRight("شكراً لثقتكم", bodyFont);
                e.HasMorePages = false;
            };

            using var dialog = new PrintDialog { Document = printDoc, UseEXDialog = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                printDoc.Print();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "خطأ في الطباعة",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static string GetRowValue(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
            return string.Empty;

        return Convert.ToString(row[column]) ?? string.Empty;
    }

    private static decimal GetDecimalValue(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
            return 0m;

        return Convert.ToDecimal(row[column]);
    }

    private void ExportInvoices()
    {
        if (!_access.AllowExport || _invoicesData == null) return;
        
        using var dialog = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = "Invoices.csv", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        
        var sb = new System.Text.StringBuilder();
        
        // Header
        var headers = new[] { "رقم", "التاريخ", "العميل/المورد", "الإجمالي", "الضريبة", "الصافي" };
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
        _status.Text = "تم التصدير إلى: " + dialog.FileName;
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
        Text = _invoiceId.HasValue ? "تعديل الفاتورة" : "فاتورة جديدة";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label
        {
            Text = _invoiceId.HasValue ? "تعديل الفاتورة" : "فاتورة جديدة",
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

        AddField(formPanel, "رقم الفاتورة", _invoiceNumberText, 0, 0);
        AddField(formPanel, "التاريخ", _datePicker, 1, 0);
        formPanel.SetColumnSpan(_datePicker, 2);
        AddField(formPanel, _salesMode ? "العميل" : "المورد", _customerSupplierText, 0, 1);
        formPanel.SetColumnSpan(_customerSupplierText, 3);
        AddField(formPanel, "ملاحظات", _notesText, 0, 2, true);
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
        
        AddField(totalsLayout, "الإجمالي", _totalText, 0, 0);
        AddField(totalsLayout, "الضريبة", _taxText, 1, 0);
        AddField(totalsLayout, "الصافي", _netText, 2, 0);
        
        totalsPanel.Controls.Add(totalsLayout);

        // Buttons Panel
        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8), WrapContents = false };
        
        _saveBtn.Text = "حفظ";
        _saveBtn.Width = 100;
        _saveBtn.Height = 36;
        _saveBtn.DialogResult = DialogResult.OK;
        _saveBtn.Click += async (_, _) => await SaveInvoiceAsync();
        
        _cancelBtn.Text = "إلغاء";
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
                var invoice = await _invoiceService.GetInvoiceByIdAsync(_invoiceId.Value, _session);
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
                        _invoiceDetails = await _invoiceService.GetInvoiceDetailsAsync(_invoiceId.Value, _session);
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
            MessageBox.Show(this, ex.GetBaseException().Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                
                await _invoiceService.UpdateInvoiceAsync(invoice, _session, _access.Id);
                MessageBox.Show(this, "تم تحديث الفاتورة.", "الفواتير", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(this, $"تم إنشاء الفاتورة رقم: {invoiceId}", "الفواتير", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }
}
