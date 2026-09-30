using System.Data;
using System.Drawing.Printing;
using System.Net.NetworkInformation;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Real ERP Cashier Form for Point of Sale operations.
/// Handles sales, receipts, and daily cashier operations.
/// </summary>
public sealed class CashierForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly CashierService _cashierService;
    private readonly ItemsService _itemsService;
    private readonly CustomerService _customerService;
    
    private readonly DataGridView _cartGrid = new();
    private readonly DataGridView _salesGrid = new();
    private readonly TextBox _barcodeText = new();
    private readonly TextBox _customerSearch = new();
    private readonly TextBox _quantityText = new();
    private readonly TextBox _priceText = new();
    private readonly TextBox _discountText = new();
    private readonly Label _customerLabel = new();
    private readonly Label _totalLabel = new();
    private readonly Label _taxLabel = new();
    private readonly Label _netLabel = new();
    private readonly Label _status = new();
    
    private DataTable? _itemsData;
    private DataTable? _customersData;
    private DataTable? _cartData;
    private int? _selectedCustomerId;
    private decimal _total = 0;
    private decimal _tax = 0;
    private decimal _net = 0;
    private const decimal _taxRate = 0.15m; // 15% VAT

    public CashierForm(AppSession session, ScreenAccess access, CashierService cashierService, ItemsService itemsService, CustomerService customerService)
    {
        _session = session;
        _access = access;
        _cashierService = cashierService;
        _itemsService = itemsService;
        _customerService = customerService;
        
        InitializeUi();
        InitializeCart();
    }

    private void InitializeUi()
    {
        Text = "\u0001\u0001 - \u0001"; // "نظام الكاشير - نقطة البيع"
        Width = 1400;
        Height = 900;
        MinimumSize = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        WindowState = FormWindowState.Maximized;

        // Header Panel
        var header = new Panel { Dock = DockStyle.Top, Height = 100, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label
        {
            Text = "\u0001", // "نظام الكاشير"
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 20, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"\u0001: {_session.UserName} | \u0001: {_session.BranchId?.ToString() ?? "-"} | \u0001: {DateTime.Now:yyyy-MM-dd}", // "المستخدم: ... | الفرع: ... | التاريخ: ..."
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(title);
        header.Controls.Add(info);

        // Customer Panel
        var customerPanel = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        var customerTitle = new Label { Text = "\u0001", Width = 100, TextAlign = ContentAlignment.MiddleRight }; // "العميل:"
        _customerSearch.Dock = DockStyle.Fill;
        _customerSearch.RightToLeft = RightToLeft.Yes;
        _customerLabel.Width = 300;
        _customerLabel.TextAlign = ContentAlignment.MiddleLeft;
        
        var customerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        customerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        customerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        customerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        customerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        customerLayout.Controls.Add(customerTitle, 0, 0);
        customerLayout.Controls.Add(_customerSearch, 1, 0);
        customerLayout.Controls.Add(_customerLabel, 2, 0);
        var selectCustomerBtn = new Button { Text = "\u0001", Width = 100, Height = 30 }; // "اختر"
        selectCustomerBtn.Click += (_, _) => ShowCustomerSelection();
        customerLayout.Controls.Add(selectCustomerBtn, 3, 0);
        customerPanel.Controls.Add(customerLayout);

        // Main Split Container
        var splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 400 };
        
        // Top Panel - Sales Cart
        var cartPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var cartTitle = new Label { Text = "\u0001", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "سلة المشتريات"
        
        // Cart Grid
        _cartGrid.Dock = DockStyle.Fill;
        _cartGrid.ReadOnly = true;
        _cartGrid.AllowUserToAddRows = false;
        _cartGrid.AllowUserToDeleteRows = true;
        _cartGrid.AutoGenerateColumns = false;
        _cartGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _cartGrid.RightToLeft = RightToLeft.Yes;
        _cartGrid.RowHeadersVisible = false;
        _cartGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _cartGrid.CellClick += (_, e) => { if (e.RowIndex >= 0 && _cartGrid.Columns[e.ColumnIndex].Name == "Delete") RemoveFromCart(e.RowIndex); };
        
        // Add columns to cart grid
        _cartGrid.Columns.Add("Delete", "\u0001"); // "حذف"
        _cartGrid.Columns["Delete"].Width = 60;
        _cartGrid.Columns["Delete"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _cartGrid.Columns.Add("ItemName", "\u0001"); // "المنتج"
        _cartGrid.Columns.Add("Barcode", "\u0001"); // "الباركود"
        _cartGrid.Columns.Add("Quantity", "\u0001"); // "الكمية"
        _cartGrid.Columns["Quantity"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _cartGrid.Columns.Add("Price", "\u0001"); // "السعر"
        _cartGrid.Columns["Price"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _cartGrid.Columns.Add("Discount", "\u0001"); // "الخصم"
        _cartGrid.Columns["Discount"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _cartGrid.Columns.Add("Total", "\u0001"); // "الإجمالي"
        _cartGrid.Columns["Total"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        
        cartPanel.Controls.Add(_cartGrid);
        cartPanel.Controls.Add(cartTitle);
        
        // Cart Controls Panel
        var cartControls = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        var itemLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
        itemLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        itemLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        itemLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        itemLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        itemLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        itemLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        
        var barcodeLabel = new Label { Text = "\u0001", Width = 100, TextAlign = ContentAlignment.MiddleRight }; // "باركود:"
        _barcodeText.Dock = DockStyle.Fill;
        _barcodeText.RightToLeft = RightToLeft.Yes;
        _barcodeText.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) AddItemByBarcode(); };
        
        var quantityLabel = new Label { Text = "\u0001", Width = 60, TextAlign = ContentAlignment.MiddleRight }; // "كمية:"
        _quantityText.Width = 60;
        _quantityText.Text = "1";
        _quantityText.TextAlign = HorizontalAlignment.Right;
        
        var priceLabel = new Label { Text = "\u0001", Width = 60, TextAlign = ContentAlignment.MiddleRight }; // "سعر:"
        _priceText.Width = 60;
        _priceText.TextAlign = HorizontalAlignment.Right;
        
        var discountLabel = new Label { Text = "\u0001", Width = 60, TextAlign = ContentAlignment.MiddleRight }; // "خصم:"
        _discountText.Width = 60;
        _discountText.Text = "0";
        _discountText.TextAlign = HorizontalAlignment.Right;
        
        var addBtn = new Button { Text = "\u0001", Width = 100, Height = 30 }; // "إضافة"
        addBtn.Click += (_, _) => AddItemToCart();
        
        itemLayout.Controls.Add(barcodeLabel, 0, 0);
        itemLayout.Controls.Add(_barcodeText, 1, 0);
        itemLayout.Controls.Add(quantityLabel, 2, 0);
        itemLayout.Controls.Add(_quantityText, 3, 0);
        itemLayout.Controls.Add(priceLabel, 4, 0);
        itemLayout.Controls.Add(_priceText, 5, 0);
        cartControls.Controls.Add(itemLayout);
        cartPanel.Controls.Add(cartControls);
        
        splitContainer.Panel1.Controls.Add(cartPanel);

        // Bottom Panel - Sales History
        var salesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var salesTitle = new Label { Text = "\u0001", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "آخر المبيعات"
        
        _salesGrid.Dock = DockStyle.Fill;
        _salesGrid.ReadOnly = true;
        _salesGrid.AllowUserToAddRows = false;
        _salesGrid.AllowUserToDeleteRows = false;
        _salesGrid.AutoGenerateColumns = true;
        _salesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _salesGrid.RightToLeft = RightToLeft.Yes;
        _salesGrid.RowHeadersVisible = false;
        _salesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _salesGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await ShowSaleDetailsAsync(); };
        
        salesPanel.Controls.Add(_salesGrid);
        salesPanel.Controls.Add(salesTitle);
        splitContainer.Panel2.Controls.Add(salesPanel);

        // Totals Panel
        var totalsPanel = new Panel { Dock = DockStyle.Right, Width = 250, Padding = new Padding(8), BackColor = Color.FromArgb(240, 248, 255) };
        
        var totalsTitle = new Label { Text = "\u0001", Dock = DockStyle.Top, Height = 30, Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }; // "الإجمالي"
        
        var totalLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
        totalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        totalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        totalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        totalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        totalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        totalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        totalLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        
        var subtotalLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "المبلغ:"
        _totalLabel.Text = "0.00";
        _totalLabel.TextAlign = ContentAlignment.MiddleLeft;
        _totalLabel.Font = new Font("Tahoma", 12, FontStyle.Bold);
        
        var taxLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "الضريبة:"
        _taxLabel.Text = "0.00";
        _taxLabel.TextAlign = ContentAlignment.MiddleLeft;
        _taxLabel.Font = new Font("Tahoma", 12, FontStyle.Bold);
        
        var netLabel = new Label { Text = "\u0001", TextAlign = ContentAlignment.MiddleRight }; // "الصافي:"
        _netLabel.Text = "0.00";
        _netLabel.TextAlign = ContentAlignment.MiddleLeft;
        _netLabel.Font = new Font("Tahoma", 14, FontStyle.Bold);
        _netLabel.ForeColor = Color.Blue;
        
        totalLayout.Controls.Add(subtotalLabel, 0, 0);
        totalLayout.Controls.Add(_totalLabel, 1, 0);
        totalLayout.Controls.Add(taxLabel, 0, 1);
        totalLayout.Controls.Add(_taxLabel, 1, 1);
        totalLayout.Controls.Add(netLabel, 0, 2);
        totalLayout.Controls.Add(_netLabel, 1, 2);
        totalLayout.SetColumnSpan(_netLabel, 2);
        
        // Action Buttons
        var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 120, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(4) };
        
        var completeBtn = new Button { Text = "\u0001", Width = 120, Height = 40, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, Font = new Font("Tahoma", 10, FontStyle.Bold) };
        completeBtn.Click += async (_, _) => await CompleteSaleAsync();
        
        var printBtn = new Button { Text = "\u0001", Width = 100, Height = 40 }; // "طباعة"
        printBtn.Click += (_, _) => PrintReceipt();
        
        var clearBtn = new Button { Text = "\u0001", Width = 100, Height = 40 }; // "مسح السلة"
        clearBtn.Click += (_, _) => ClearCart();
        
        var newSaleBtn = new Button { Text = "\u0001", Width = 100, Height = 40 }; // "بيع جديد"
        newSaleBtn.Click += (_, _) => NewSale();
        
        actionPanel.Controls.Add(completeBtn);
        actionPanel.Controls.Add(printBtn);
        actionPanel.Controls.Add(clearBtn);
        actionPanel.Controls.Add(newSaleBtn);
        
        totalsPanel.Controls.Add(totalsTitle);
        totalsPanel.Controls.Add(totalLayout);
        totalsPanel.Controls.Add(actionPanel);
        
        // Status Bar
        _status.Dock = DockStyle.Bottom;
        _status.Height = 32;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Padding = new Padding(8);
        _status.BorderStyle = BorderStyle.FixedSingle;
        _status.BackColor = Color.FromArgb(220, 230, 241);

        Controls.Add(header);
        Controls.Add(customerPanel);
        Controls.Add(splitContainer);
        Controls.Add(totalsPanel);
        Controls.Add(_status);

        // Load initial data
        Shown += async (_, _) => await LoadInitialDataAsync();
        _customerSearch.TextChanged += async (_, _) => await SearchCustomersAsync();
    }

    private void InitializeCart()
    {
        _cartData = new DataTable();
        _cartData.Columns.Add("ItemID", typeof(int));
        _cartData.Columns.Add("ItemName", typeof(string));
        _cartData.Columns.Add("Barcode", typeof(string));
        _cartData.Columns.Add("Quantity", typeof(decimal));
        _cartData.Columns.Add("Price", typeof(decimal));
        _cartData.Columns.Add("Discount", typeof(decimal));
        _cartData.Columns.Add("Total", typeof(decimal));
        _cartGrid.DataSource = _cartData;
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            UseWaitCursor = true;
            _itemsData = await _itemsService.ListAsync();
            _customersData = await _customerService.ListAsync(_session.BranchId);
            await LoadSalesAsync();
            _status.Text = "\u0001"; // "النظام جاهز"
        }
        catch (Exception ex)
        {
            _status.Text = "\u0001: " + ex.GetBaseException().Message; // "خطأ: "
        }
        finally { UseWaitCursor = false; }
    }

    private async Task LoadSalesAsync(DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.Today;
            _salesGrid.DataSource = await _cashierService.GetSalesAsync(_session.BranchId, targetDate, targetDate);
            FormatSalesGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ"
        }
    }

    private void FormatSalesGrid()
    {
        if (_salesGrid.Columns.Count == 0) return;
        
        // Hide unnecessary columns
        var hiddenColumns = new[] { "ID", "PurBranchID", "SupplierID", "SupplierVatNum", "UserID_Add", "UserBranch_Add", 
                                     "UserMacAddress_Add", "UserDate_Add", "UserID_Update", "UserBranch_Update", 
                                     "UserMacAddress_Update", "UserDate_Update", "RestBraID", "RestBraISClosed",
                                     "BounceID", "Res_Delivery_ID", "BuildingNum", "Street", "District", "City",
                                     "Country", "PostalCode", "AdditionalNum", "CommercialRecord", "ProjectId",
                                     "YearId", "HasmPer", "HasmAmount", "Sectoral", "SalesMan", "DateHold",
                                     "Charge", "CashAcc", "BankAcc", "Motbqy", "CashRecipt", "BankRecipt",
                                     "CashReciptAcc", "BankReciptAcc", "DateRecipt", "AllDiscount", "BounsAmount",
                                     "DisCode", "QRCode", "OrderTypeElectronicInvoiceId", "NormalOrSimpleInvoice",
                                     "ThirdParty", "NominalInvoice", "ExportInvoice", "SummaryInvoice", "SelfInvoice",
                                     "AmountPaid", "Rest", "CarName", "CarModel", "PlateNumber", "ChassisNum",
                                     "CarColor", "Counter" };
        
        foreach (var colName in hiddenColumns)
        {
            if (_salesGrid.Columns.Contains(colName))
                _salesGrid.Columns[colName].Visible = false;
        }
        
        // Rename columns
        if (_salesGrid.Columns.Contains("NoteNum")) _salesGrid.Columns["NoteNum"].HeaderText = "\u0001"; // "رقم الفاتورة"
        if (_salesGrid.Columns.Contains("SupplierName")) _salesGrid.Columns["SupplierName"].HeaderText = "\u0001"; // "العميل"
        if (_salesGrid.Columns.Contains("Purchases_Date")) _salesGrid.Columns["Purchases_Date"].HeaderText = "\u0001"; // "التاريخ"
        if (_salesGrid.Columns.Contains("TotalPrices")) _salesGrid.Columns["TotalPrices"].HeaderText = "\u0001"; // "الإجمالي"
        if (_salesGrid.Columns.Contains("Net")) _salesGrid.Columns["Net"].HeaderText = "\u0001"; // "الصافي"
    }

    private async Task SearchCustomersAsync()
    {
        var searchTerm = _customerSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _customersData = await _customerService.ListAsync(_session.BranchId);
            _customerLabel.Text = string.Empty;
            _selectedCustomerId = null;
            return;
        }
        
        _customersData = await _customerService.SearchAsync(searchTerm, _session.BranchId);
        if (_customersData.Rows.Count == 1)
        {
            _selectedCustomerId = Convert.ToInt32(_customersData.Rows[0]["ID"]);
            _customerLabel.Text = Convert.ToString(_customersData.Rows[0]["CustSuppName"]);
        }
    }

    private void ShowCustomerSelection()
    {
        if (_customersData == null || _customersData.Rows.Count == 0)
        {
            MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "لا يوجد عملاء"
            return;
        }
        
        using var form = new Form
        {
            Text = "\u0001", // "اختر عميل"
            Width = 600,
            Height = 400,
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes
        };
        
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RightToLeft = RightToLeft.Yes,
            DataSource = _customersData,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        
        var selectBtn = new Button { Text = "\u0001", Width = 100, Height = 30, DialogResult = DialogResult.OK }; // "اختر"
        selectBtn.Dock = DockStyle.Bottom;
        selectBtn.Click += (_, _) =>
        {
            if (grid.CurrentRow?.DataBoundItem is DataRowView row)
            {
                _selectedCustomerId = Convert.ToInt32(row.Row["ID"]);
                _customerLabel.Text = Convert.ToString(row.Row["CustSuppName"]);
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        };
        
        form.Controls.Add(grid);
        form.Controls.Add(selectBtn);
        form.Shown += (_, _) => grid.Focus();
        
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _customerSearch.Text = _customerLabel.Text;
        }
    }

    private void AddItemByBarcode()
    {
        var barcode = _barcodeText.Text.Trim();
        if (string.IsNullOrWhiteSpace(barcode)) return;
        
        if (_itemsData == null) return;
        
        var row = _itemsData.Select("Item_code = '" + barcode.Replace("'", "''") + "'").FirstOrDefault();
        if (row != null)
        {
            AddItemToCart(row);
        }
        else
        {
            MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Warning); // "الباركود غير موجود"
        }
    }

    private void AddItemToCart()
    {
        if (_itemsData == null) return;
        
        var itemName = _barcodeText.Text.Trim();
        if (string.IsNullOrWhiteSpace(itemName)) return;
        
        // Search by name
        var rows = _itemsData.Select("item_Name LIKE '%" + itemName.Replace("'", "''") + "%'");
        if (rows.Length == 0)
        {
            MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Warning); // "المنتج غير موجود"
            return;
        }
        
        // Use first match
        AddItemToCart(rows[0]);
    }

    private void AddItemToCart(DataRow itemRow)
    {
        if (_cartData == null) return;
        
        var itemId = Convert.ToInt32(itemRow["ItemId"]);
        var itemName = Convert.ToString(itemRow["item_Name"]);
        var barcode = Convert.ToString(itemRow["Item_code"]);
        
        decimal price = 0;
        if (itemRow["SellPriceSmall"] != DBNull.Value)
            price = Convert.ToDecimal(itemRow["SellPriceSmall"]);
        
        decimal quantity = 1;
        if (!string.IsNullOrWhiteSpace(_quantityText.Text) && decimal.TryParse(_quantityText.Text, out var qty))
            quantity = qty;
        
        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(_discountText.Text) && decimal.TryParse(_discountText.Text, out var disc))
            discount = disc;
        
        // Check if item already in cart
        foreach (DataRow cartRow in _cartData.Rows)
        {
            if (Convert.ToInt32(cartRow["ItemID"]) == itemId)
            {
                cartRow["Quantity"] = Convert.ToDecimal(cartRow["Quantity"]) + quantity;
                cartRow["Total"] = CalculateItemTotal(price, Convert.ToDecimal(cartRow["Quantity"]), discount);
                UpdateTotals();
                ClearItemInputs();
                return;
            }
        }
        
        // Add new item to cart
        var newRow = _cartData.NewRow();
        newRow["ItemID"] = itemId;
        newRow["ItemName"] = itemName;
        newRow["Barcode"] = barcode;
        newRow["Quantity"] = quantity;
        newRow["Price"] = price;
        newRow["Discount"] = discount;
        newRow["Total"] = CalculateItemTotal(price, quantity, discount);
        _cartData.Rows.Add(newRow);
        
        UpdateTotals();
        ClearItemInputs();
        _barcodeText.Focus();
    }

    private decimal CalculateItemTotal(decimal price, decimal quantity, decimal discount)
    {
        return (price * quantity) - discount;
    }

    private void RemoveFromCart(int rowIndex)
    {
        if (_cartData == null || rowIndex < 0 || rowIndex >= _cartData.Rows.Count) return;
        
        _cartData.Rows.RemoveAt(rowIndex);
        UpdateTotals();
    }

    private void ClearItemInputs()
    {
        _barcodeText.Clear();
        _quantityText.Text = "1";
        _priceText.Clear();
        _discountText.Text = "0";
    }

    private void ClearCart()
    {
        if (_cartData != null)
            _cartData.Clear();
        _total = 0;
        _tax = 0;
        _net = 0;
        UpdateTotalsDisplay();
    }

    private void NewSale()
    {
        ClearCart();
        _customerLabel.Text = string.Empty;
        _selectedCustomerId = null;
        _customerSearch.Clear();
        _barcodeText.Focus();
    }

    private void UpdateTotals()
    {
        if (_cartData == null) return;
        
        _total = 0;
        foreach (DataRow row in _cartData.Rows)
        {
            _total += Convert.ToDecimal(row["Total"]);
        }
        
        _tax = _total * _taxRate;
        _net = _total + _tax;
        
        UpdateTotalsDisplay();
    }

    private void UpdateTotalsDisplay()
    {
        _totalLabel.Text = _total.ToString("N2");
        _taxLabel.Text = _tax.ToString("N2");
        _netLabel.Text = _net.ToString("N2");
    }

    private async Task CompleteSaleAsync()
    {
        if (_cartData == null || _cartData.Rows.Count == 0)
        {
            MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Warning); // "السلة فارغة"
            return;
        }
        
        if (!_selectedCustomerId.HasValue)
        {
            MessageBox.Show(this, "\u0001", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Warning); // "يرجى اختيار عميل"
            return;
        }
        
        // Create order
        var order = new Order_Orders
        {
            SupplierName = _customerLabel.Text,
            SupplierID = _selectedCustomerId.Value,
            Purchases_Date = DateTime.Now,
            CostOrder = _total,
            Tax = _tax,
            TotalPrices = _total,
            Net = _net,
            OrderCashierType = true,
            CashMoney = _net, // Full payment by cash
            Note = "\u0001" // "مبيعات كاشير"
        };
        
        // Create details
        var details = new List<Order_OrdersDetails>();
        foreach (DataRow row in _cartData.Rows)
        {
            details.Add(new Order_OrdersDetails
            {
                ItemID = Convert.ToInt32(row["ItemID"]),
                Quantity = Convert.ToDecimal(row["Quantity"]),
                UnitPrice = Convert.ToDecimal(row["Price"]),
                TotalPrice = Convert.ToDecimal(row["Total"]),
                VAT = Convert.ToDecimal(row["Price"]) * _taxRate, // VAT per item
                ItemUnitType = "\u0001", // "قطعة"
                IsPrint = true
            });
        }
        
        try
        {
            UseWaitCursor = true;
            var saleId = await _cashierService.CreateSaleAsync(order, details, _session);
            
            // Print receipt
            PrintReceipt(saleId);
            
            // Clear and reload
            ClearCart();
            await LoadSalesAsync();
            
            _status.Text = $"\u0001 {saleId}"; // "تم البيع رقم: ..."
            MessageBox.Show(this, $"\u0001 {saleId}", "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Information); // "تم البيع بنجاح رقم: ..."
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ في البيع"
        }
        finally { UseWaitCursor = false; }
    }

    private async Task ShowSaleDetailsAsync()
    {
        if (_salesGrid.CurrentRow?.DataBoundItem is not DataRowView row)
            return;
        
        var saleId = Convert.ToInt32(row.Row["ID"]);
        var details = await _cashierService.GetSaleDetailsAsync(saleId);
        
        using var form = new Form
        {
            Text = $"\u0001 {saleId}", // "تفاصيل البيع رقم: ..."
            Width = 800,
            Height = 500,
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes
        };
        
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RightToLeft = RightToLeft.Yes,
            DataSource = details
        };
        
        var closeBtn = new Button { Text = "\u0001", Width = 100, Height = 30, DialogResult = DialogResult.OK }; // "إغلاق"
        closeBtn.Dock = DockStyle.Bottom;
        closeBtn.Click += (_, _) => form.Close();
        
        form.Controls.Add(grid);
        form.Controls.Add(closeBtn);
        form.ShowDialog(this);
    }

    private void PrintReceipt(int? saleId = null)
    {
        try
        {
            var printDoc = new PrintDocument();
            printDoc.PrintPage += (sender, e) =>
            {
                var font = new Font("Tahoma", 12);
                var boldFont = new Font("Tahoma", 14, FontStyle.Bold);
                var smallFont = new Font("Tahoma", 10);
                
                float yPos = 20;
                float x = e.MarginBounds.Left;
                float y = yPos;
                
                // Header
                e.Graphics.DrawString("\u0001", boldFont, Brushes.Black, x, y); // "شركة السقر"
                y += 30;
                e.Graphics.DrawString("\u0001", font, Brushes.Black, x, y); // "فاتورة بيع"
                y += 25;
                e.Graphics.DrawString($"\u0001: {DateTime.Now:yyyy-MM-dd HH:mm}", smallFont, Brushes.Black, x, y); // "التاريخ: ..."
                y += 20;
                e.Graphics.DrawString($"\u0001: {_customerLabel.Text}", font, Brushes.Black, x, y); // "العميل: ..."
                y += 25;
                
                // Items
                e.Graphics.DrawString("\u0001", font, Brushes.Black, x, y); // "المنتج"
                e.Graphics.DrawString("\u0001", font, Brushes.Black, x + 300, y); // "الكمية"
                e.Graphics.DrawString("\u0001", font, Brushes.Black, x + 400, y); // "السعر"
                e.Graphics.DrawString("\u0001", font, Brushes.Black, x + 500, y); // "الإجمالي"
                y += 25;
                
                if (_cartData != null)
                {
                    foreach (DataRow row in _cartData.Rows)
                    {
                        var itemName = Convert.ToString(row["ItemName"]);
                        var quantity = Convert.ToString(row["Quantity"]);
                        var price = Convert.ToDecimal(row["Price"]).ToString("N2");
                        var total = Convert.ToDecimal(row["Total"]).ToString("N2");
                        
                        e.Graphics.DrawString(itemName, smallFont, Brushes.Black, x, y);
                        e.Graphics.DrawString(quantity, smallFont, Brushes.Black, x + 300, y);
                        e.Graphics.DrawString(price, smallFont, Brushes.Black, x + 400, y);
                        e.Graphics.DrawString(total, smallFont, Brushes.Black, x + 500, y);
                        y += 20;
                    }
                }
                
                y += 20;
                e.Graphics.DrawString("\u0001", font, Brushes.Black, x, y); // "الإجمالي:"
                e.Graphics.DrawString(_total.ToString("N2"), font, Brushes.Black, x + 500, y);
                y += 25;
                e.Graphics.DrawString("\u0001", font, Brushes.Black, x, y); // "الضريبة:"
                e.Graphics.DrawString(_tax.ToString("N2"), font, Brushes.Black, x + 500, y);
                y += 25;
                e.Graphics.DrawString("\u0001", boldFont, Brushes.Black, x, y); // "الصافي:"
                e.Graphics.DrawString(_net.ToString("N2"), boldFont, Brushes.Black, x + 500, y);
                
                y += 40;
                e.Graphics.DrawString("\u0001", smallFont, Brushes.Black, x, y); // "شكرا لثقتكم"
            };
            
            var printDialog = new PrintDialog { Document = printDoc };
            if (printDialog.ShowDialog(this) == DialogResult.OK)
            {
                printDoc.Print();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "\u0001", MessageBoxButtons.OK, MessageBoxIcon.Error); // "خطأ في الطباعة"
        }
    }

    private static string GetMachineMac()
    {
        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Select(n => n.GetPhysicalAddress()?.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return string.IsNullOrWhiteSpace(mac) ? Environment.MachineName : mac;
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
