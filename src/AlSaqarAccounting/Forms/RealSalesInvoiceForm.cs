using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Real sales invoice entry for GTSdb2026.
/// Header/lines are validated in the application and persisted through
/// SalesService -> dbo.Insert_Order_Order_ALL using dbo.Items_Orders.
/// </summary>
public sealed class RealSalesInvoiceForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly SalesService _sales;
    private readonly StoresService _stores;

    private readonly ComboBox _customer = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _store = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _payment = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _item = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, RightToLeft = RightToLeft.Yes };
    private readonly DateTimePicker _date = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
    private readonly TextBox _number = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _note = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _lineNote = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly NumericUpDown _qty = new() { Dock = DockStyle.Fill, Minimum = 0.001m, Maximum = 999999999m, DecimalPlaces = 3, Value = 1m };
    private readonly NumericUpDown _price = new() { Dock = DockStyle.Fill, Minimum = 0m, Maximum = 999999999m, DecimalPlaces = 2 };
    private readonly NumericUpDown _lineDiscount = new() { Dock = DockStyle.Fill, Minimum = 0m, Maximum = 999999999m, DecimalPlaces = 2 };
    private readonly NumericUpDown _discount = new() { Dock = DockStyle.Fill, Minimum = 0m, Maximum = 999999999m, DecimalPlaces = 2 };
    private readonly NumericUpDown _paid = new() { Dock = DockStyle.Fill, Minimum = 0m, Maximum = 999999999m, DecimalPlaces = 2 };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RightToLeft = RightToLeft.Yes
    };

    private readonly Label _info = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.DimGray };
    private readonly Label _totals = new()
    {
        Dock = DockStyle.Bottom,
        Height = 58,
        TextAlign = ContentAlignment.MiddleRight,
        Font = new Font("Tahoma", 10, FontStyle.Bold),
        Padding = new Padding(8)
    };

    private readonly List<Order_OrdersDetails> _lines = new();
    private DataTable? _customers;
    private DataTable? _items;
    private DataTable? _stores;
    private string? _customerId;
    private string? _customerName;
    private string? _customerPhone;
    private string? _customerVat;
    private string? _itemId;
    private string? _itemName;
    private string? _itemPrice;
    private string? _storeId;
    private string? _storeName;
    private decimal _vatRate;
    private bool _vatEnabled;
    private int? _editingIndex;

    public bool Saved { get; private set; }

    public RealSalesInvoiceForm(
        AppSession session,
        ScreenAccess access,
        SalesService sales,
        StoresService stores)
    {
        _session = session;
        _access = access;
        _sales = sales;
        _stores = stores;

        Text = "الصقر للمحاسبة — فاتورة مبيعات";
        Width = 1280;
        Height = 830;
        MinimumSize = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        _payment.Items.Add(new PaymentType(1, "نقدي"));
        _payment.Items.Add(new PaymentType(2, "بنك"));
        _payment.Items.Add(new PaymentType(3, "آجل"));
        _payment.SelectedIndex = 0;

        BuildLayout();

        _item.SelectedValueChanged += (_, _) => LoadSelectedItemPrice();
        _customer.SelectedValueChanged += (_, _) => LoadSelectedCustomerInfo();
        _qty.ValueChanged += (_, _) => UpdateTotals();
        _price.ValueChanged += (_, _) => UpdateTotals();
        _lineDiscount.ValueChanged += (_, _) => UpdateTotals();
        _discount.ValueChanged += (_, _) => UpdateTotals();
        _paid.ValueChanged += (_, _) => UpdateTotals();
        _payment.SelectedIndexChanged += (_, _) => UpdatePaidForPaymentType();
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildLayout()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };

        var save = new Button { Text = "حفظ الفاتورة", Width = 130, Height = 34, Enabled = _access.AllowSave };
        save.Click += async (_, _) => await SaveAsync();
        var close = new Button { Text = "إغلاق", Width = 90, Height = 34 };
        close.Click += (_, _) => Close();
        buttons.Controls.Add(save);
        buttons.Controls.Add(close);

        var header = new Panel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(10), BackColor = Color.FromArgb(245, 247, 250) };
        header.Controls.Add(_info);
        header.Controls.Add(new Label
        {
            Text = "فاتورة مبيعات",
            Dock = DockStyle.Top,
            Height = 32,
            Font = new Font("Tahoma", 17, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        });

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var head = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2 };
        for (var i = 0; i < 6; i++) head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6667f));
        AddField(head, "العميل", _customer, 0, 0, 2);
        AddField(head, "المخزن", _store, 2, 0);
        AddField(head, "نوع الدفع", _payment, 3, 0);
        AddField(head, "التاريخ", _date, 4, 0);
        AddField(head, "رقم الفاتورة", _number, 5, 0);
        AddField(head, "ملاحظات", _note, 0, 1, 6);
        body.Controls.Add(head, 0, 0);

        var line = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 2 };
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
        AddField(line, "الصنف", _item, 0, 0);
        AddField(line, "الكمية", _qty, 1, 0);
        AddField(line, "السعر", _price, 2, 0);
        AddField(line, "خصم السطر", _lineDiscount, 3, 0);
        AddField(line, "ملاحظة", _lineNote, 4, 0);

        var lineButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var add = new Button { Text = "إضافة", Width = 90, Height = 30 };
        var edit = new Button { Text = "تعديل", Width = 90, Height = 30, Enabled = false };
        var remove = new Button { Text = "حذف", Width = 80, Height = 30, Enabled = false };
        add.Click += (_, _) => AddOrUpdateLine();
        edit.Click += (_, _) => StartEditLine();
        remove.Click += (_, _) => RemoveLine();
        lineButtons.Controls.Add(add);
        lineButtons.Controls.Add(edit);
        lineButtons.Controls.Add(remove);
        line.Controls.Add(lineButtons, 5, 0);
        line.SetColumnSpan(lineButtons, 2);
        body.Controls.Add(line, 0, 1);

        var grids = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
        grids.Controls.Add(_grid);
        body.Controls.Add(grids, 0, 2);

        var discountPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        discountPanel.Controls.Add(new Label { Text = "خصم الفاتورة", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 8, 0, 0) });
        _discount.Width = 130;
        discountPanel.Controls.Add(_discount);
        discountPanel.Controls.Add(new Label { Text = "المدفوع", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 8, 0, 0) });
        _paid.Width = 130;
        discountPanel.Controls.Add(_paid);

        Controls.Add(body);
        Controls.Add(_totals);
        Controls.Add(discountPanel);
        Controls.Add(buttons);
        Controls.Add(header);

        _grid.SelectionChanged += (_, _) =>
        {
            var has = SelectedLineIndex().HasValue;
            edit.Enabled = has;
            remove.Enabled = has;
        };
    }

    private static void AddField(TableLayoutPanel p, string label, Control c, int col, int row, int span = 1)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(3) };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Tahoma", 9, FontStyle.Bold) }, 0, 0);
        host.Controls.Add(c, 0, 1);
        p.Controls.Add(host, col, row);
        if (span > 1) p.SetColumnSpan(host, span);
    }

    private async Task LoadAsync()
    {
        try
        {
            UseWaitCursor = true;
            if (!_session.BranchId.HasValue)
                throw new InvalidOperationException("الحساب الحالي لا يحتوي على فرع.");

            var settingTask = _sales.GetEntrySettingsAsync(_session.BranchId.Value);
            var customerTask = _sales.ListCustomersAsync(_session.BranchId);
            var itemTask = _sales.ListItemsAsync();
            var storeTask = _stores.ListAsync();
            await Task.WhenAll(settingTask, customerTask, itemTask, storeTask);

            var settings = settingTask.Result;
            _vatEnabled = settings.VatEnabled;
            _vatRate = settings.VatRate;
            _customers = customerTask.Result;
            _items = itemTask.Result;
            _stores = storeTask.Result;

            _customerId = FindColumn(_customers, "ID", "SN", "AccountID");
            _customerName = FindColumn(_customers, "Name", "CustSuppName", "Account_Name", "CustomerName");
            _customerPhone = FindColumn(_customers, "Phone", "Mobile", "CustSuppPhone", "SupplierPhone");
            _customerVat = FindColumn(_customers, "VatNum", "VATNumber", "SupplierVatNum");
            _itemId = FindColumn(_items, "ItemId", "ItemID", "ID", "SN");
            _itemName = FindColumn(_items, "item_Name", "ItemName", "Name", "Item_Name");
            _itemPrice = FindColumn(_items, "SellPriceSmall", "SellPrice", "Price", "UnitPrice");
            _storeId = FindColumn(_stores, "ID", "SN", "StoreID");
            _storeName = FindColumn(_stores, "Store_Name", "Name", "StoreName");

            if (_customerId is null || _customerName is null) throw new InvalidOperationException("بيانات العملاء من Select_AccountCustomer غير مكتملة.");
            if (_itemId is null || _itemName is null) throw new InvalidOperationException("بيانات الأصناف من Get_All_Items غير مكتملة.");
            if (_storeId is null || _storeName is null) throw new InvalidOperationException("بيانات المخازن غير مكتملة.");

            Bind(_customer, _customers, _customerId, _customerName, true);
            Bind(_item, _items, _itemId, _itemName, true);
            Bind(_store, _stores, _storeId, _storeName, false);

            SelectStore(settings.DefaultStoreId);
            _date.Value = DateTime.Today;
            _info.Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId} | الضريبة: {(_vatEnabled ? (_vatRate * 100m).ToString("0.##") + "%" : "غير مفعلة")}";
            UpdateTotals();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "فاتورة مبيعات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private static void Bind(ComboBox combo, DataTable table, string id, string name, bool editable)
    {
        combo.DataSource = null;
        combo.DisplayMember = name;
        combo.ValueMember = id;
        combo.DataSource = new BindingSource(table, null);
        combo.DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList;

        if (!editable)
        {
            combo.AutoCompleteMode = AutoCompleteMode.None;
            combo.AutoCompleteSource = AutoCompleteSource.None;
            combo.AutoCompleteCustomSource = null;
            return;
        }

        var source = new AutoCompleteStringCollection();
        foreach (DataRow r in table.Rows)
            if (!r.IsNull(name))
            {
                var v = Convert.ToString(r[name]);
                if (!string.IsNullOrWhiteSpace(v)) source.Add(v);
            }
        combo.AutoCompleteSource = AutoCompleteSource.CustomSource;
        combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        combo.AutoCompleteCustomSource = source;
    }

    private void SelectStore(int? id)
    {
        if (id.HasValue && _storeId is not null)
            for (var i = 0; i < _store.Items.Count; i++)
                if (_store.Items[i] is DataRowView r && Convert.ToInt32(r[_storeId]) == id.Value)
                { _store.SelectedIndex = i; return; }

        if (_store.Items.Count > 0) _store.SelectedIndex = 0;
    }

    private void LoadSelectedItemPrice()
    {
        if (_items is null || _itemId is null || _itemPrice is null) return;
        if (!int.TryParse(Convert.ToString(_item.SelectedValue), out var id)) return;
        var row = FindRow(_items, _itemId, id);
        if (row is not null && !row.IsNull(_itemPrice) && decimal.TryParse(Convert.ToString(row[_itemPrice]), out var value))
            _price.Value = Clamp(_price, value);
    }

    private void LoadSelectedCustomerInfo()
    {
        if (_customers is null || _customerId is null) return;
        if (!int.TryParse(Convert.ToString(_customer.SelectedValue), out var id)) return;
        var row = FindRow(_customers, _customerId, id);
        _info.Text = $"المستخدم: {_session.UserName} | العميل: {_customer.Text} | الهاتف: {Value(row, _customerPhone) ?? "-"} | الرقم الضريبي: {Value(row, _customerVat) ?? "-"}";
    }

    private void AddOrUpdateLine()
    {
        if (!int.TryParse(Convert.ToString(_item.SelectedValue), out var itemId) || itemId <= 0)
        { MessageBox.Show(this, "اختر الصنف."); return; }

        var storeId = TryId(_store);
        if (!storeId.HasValue) { MessageBox.Show(this, "اختر المخزن."); return; }
        if (_qty.Value <= 0) { MessageBox.Show(this, "الكمية يجب أن تكون أكبر من صفر."); return; }
        if (_price.Value <= 0) { MessageBox.Show(this, "السعر يجب أن يكون أكبر من صفر."); return; }

        var gross = decimal.Round(_qty.Value * _price.Value, 6);
        var discount = Math.Min(_lineDiscount.Value, gross);
        var taxable = decimal.Round(gross - discount, 6);
        var vat = _vatEnabled ? decimal.Round(taxable * _vatRate, 6) : 0m;

        var line = new Order_OrdersDetails
        {
            SN = (_editingIndex ?? _lines.Count) + 1,
            ItemID = itemId,
            BranchID = _session.BranchId,
            StoreID = storeId,
            Quantity = _qty.Value,
            SmallUnitPrice = _price.Value,
            UnitPrice = _price.Value,
            TotalPrice = taxable,
            VAT = vat,
            NetUnitPrice = _price.Value,
            NetTotalPrice = taxable,
            VAT_Discount = 0m,
            ItemUnitType = "Small",
            IsWaiting = false,
            IsPrint = true,
            IsPrintCook = false,
            ItemNote = string.IsNullOrWhiteSpace(_lineNote.Text) ? null : _lineNote.Text.Trim(),
            UnitNumber = 1m,
            Bounce = 0m,
            Amount_Discount = discount,
            Per_Discount = gross == 0 ? 0m : decimal.Round(discount * 100m / gross, 6),
            TobaccoTax = 0m,
            TobaccoTaxDis = 0m,
            TotalWithTaxTobacco = taxable + vat
        };

        if (_editingIndex.HasValue) _lines[_editingIndex.Value] = line;
        else _lines.Add(line);
        Renumber();
        _editingIndex = null;
        _addLineReset();
        RefreshGrid();
        UpdateTotals();
    }

    private void _addLineReset()
    {
        _lineNote.Clear();
        _lineDiscount.Value = 0m;
        _qty.Value = 1m;
        _price.Value = 0m;
    }

    private void StartEditLine()
    {
        var i = SelectedLineIndex();
        if (!i.HasValue) return;
        var line = _lines[i.Value];
        Select(_item, line.ItemID);
        Select(_store, line.StoreID);
        _qty.Value = Clamp(_qty, line.Quantity ?? 1m);
        _price.Value = Clamp(_price, line.SmallUnitPrice ?? line.UnitPrice ?? 0m);
        _lineDiscount.Value = Clamp(_lineDiscount, line.Amount_Discount ?? 0m);
        _lineNote.Text = line.ItemNote ?? string.Empty;
        _editingIndex = i.Value;
    }

    private void RemoveLine()
    {
        var i = SelectedLineIndex();
        if (!i.HasValue) return;
        _lines.RemoveAt(i.Value);
        Renumber();
        RefreshGrid();
        UpdateTotals();
    }

    private int? SelectedLineIndex()
        => _grid.CurrentRow is null || _grid.CurrentRow.Index < 0 || _grid.CurrentRow.Index >= _lines.Count ? null : _grid.CurrentRow.Index;

    private void RefreshGrid()
    {
        var t = new DataTable();
        t.Columns.Add("SN", typeof(int));
        t.Columns.Add("الصنف", typeof(string));
        t.Columns.Add("المخزن", typeof(string));
        t.Columns.Add("الكمية", typeof(decimal));
        t.Columns.Add("السعر", typeof(decimal));
        t.Columns.Add("خصم السطر", typeof(decimal));
        t.Columns.Add("قبل الضريبة", typeof(decimal));
        t.Columns.Add("الضريبة", typeof(decimal));
        t.Columns.Add("الإجمالي", typeof(decimal));
        t.Columns.Add("ملاحظة", typeof(string));

        foreach (var x in _lines)
            t.Rows.Add(x.SN, ItemText(x.ItemID), StoreText(x.StoreID), x.Quantity ?? 0m, x.UnitPrice ?? 0m, x.Amount_Discount ?? 0m,
                x.TotalPrice ?? 0m, x.VAT ?? 0m, (x.TotalPrice ?? 0m) + (x.VAT ?? 0m), x.ItemNote ?? string.Empty);

        _grid.DataSource = t;
    }

    private void UpdatePaidForPaymentType()
    {
        _paid.Value = SelectedPaymentType == 3 ? 0m : Math.Min(_paid.Maximum, InvoiceNet());
        UpdateTotals();
    }

    private int SelectedPaymentType => _payment.SelectedItem is PaymentType x ? x.Id : 1;
    private decimal Subtotal() => decimal.Round(_lines.Sum(x => x.TotalPrice ?? 0m), 6);
    private decimal VatTotal() => decimal.Round(_lines.Sum(x => x.VAT ?? 0m), 6);
    private decimal InvoiceNet() => decimal.Round(Subtotal() - Math.Min(_discount.Value, Subtotal()) + VatTotal(), 6);

    private void UpdateTotals()
    {
        var subtotal = Subtotal();
        var discount = Math.Min(_discount.Value, subtotal);
        var vat = VatTotal();
        var net = decimal.Round(subtotal - discount + vat, 2);
        if (_paid.Value > net) _paid.Value = net;
        _totals.Text = $"قبل الضريبة: {subtotal:N2} | خصم الفاتورة: {discount:N2} | الضريبة: {vat:N2} | الصافي: {net:N2} | المدفوع: {_paid.Value:N2} | المتبقي: {(net - _paid.Value):N2}";
    }

    private async Task SaveAsync()
    {
        if (!_access.AllowSave) return;
        try
        {
            UseWaitCursor = true;
            if (!_session.BranchId.HasValue) throw new InvalidOperationException("لا يوجد فرع فعال.");
            if (_lines.Count == 0) throw new InvalidOperationException("لا يمكن حفظ فاتورة بدون أصناف.");

            var customerId = TryId(_customer);
            var storeId = TryId(_store);
            if (!customerId.HasValue) throw new InvalidOperationException("اختر العميل.");
            if (!storeId.HasValue) throw new InvalidOperationException("اختر المخزن.");

            var subtotal = Subtotal();
            var invoiceDiscount = Math.Min(_discount.Value, subtotal);
            var vat = VatTotal();
            var net = decimal.Round(subtotal - invoiceDiscount + vat, 6);
            var paid = Math.Min(_paid.Value, net);

            var row = FindRow(_customers, _customerId, customerId.Value);
            var invoice = new SalesInvoice
            {
                InvoiceDate = _date.Value,
                PaymentType = SelectedPaymentType,
                CustomerId = customerId.Value,
                CustomerName = _customer.Text.Trim(),
                CustomerPhone = Value(row, _customerPhone),
                CustomerVat = Value(row, _customerVat),
                StoreId = storeId.Value,
                Note = string.IsNullOrWhiteSpace(_note.Text) ? null : _note.Text.Trim(),
                NoteNum = string.IsNullOrWhiteSpace(_number.Text) ? null : _number.Text.Trim(),
                DiscountAmount = invoiceDiscount,
                AmountPaid = paid
            };

            foreach (var x in _lines)
            {
                var copy = Copy(x);
                copy.BranchID = _session.BranchId;
                copy.StoreID = storeId;
                invoice.Lines.Add(copy);
            }

            await _sales.CreateAsync(invoice, _session);
            Saved = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حفظ الفاتورة:\r\n" + ex.GetBaseException().Message, "فاتورة مبيعات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private string ItemText(int? id) => Value(FindRow(_items, _itemId, id ?? -1), _itemName) ?? string.Empty;
    private string StoreText(int? id) => Value(FindRow(_stores, _storeId, id ?? -1), _storeName) ?? string.Empty;

    private static DataRow? FindRow(DataTable? table, string? idColumn, int id)
    {
        if (table is null || string.IsNullOrWhiteSpace(idColumn)) return null;
        foreach (DataRow row in table.Rows)
            if (!row.IsNull(idColumn) && Convert.ToInt32(row[idColumn]) == id) return row;
        return null;
    }

    private static string? Value(DataRow? row, string? column)
        => row is null || string.IsNullOrWhiteSpace(column) || !row.Table.Columns.Contains(column) || row.IsNull(column) ? null : Convert.ToString(row[column]);

    private static void Select(ComboBox combo, int? id)
    {
        if (!id.HasValue || string.IsNullOrWhiteSpace(combo.ValueMember)) return;
        for (var i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i] is DataRowView row && !row.IsNull(combo.ValueMember) && Convert.ToInt32(row[combo.ValueMember]) == id.Value)
            { combo.SelectedIndex = i; return; }
    }

    private static int? TryId(ComboBox combo)
        => int.TryParse(Convert.ToString(combo.SelectedValue), out var id) && id > 0 ? id : null;

    private static decimal Clamp(NumericUpDown c, decimal v) => Math.Min(c.Maximum, Math.Max(c.Minimum, v));

    private void Renumber()
    {
        for (var i = 0; i < _lines.Count; i++) _lines[i].SN = i + 1;
    }

    private static Order_OrdersDetails Copy(Order_OrdersDetails x)
        => new()
        {
            SN=x.SN, R_ItmSN=x.R_ItmSN, R_RowId=x.R_RowId, R_ADDId=x.R_ADDId, ItemID=x.ItemID, ItemIDADD=x.ItemIDADD, BranchID=x.BranchID,
            IsWaiting=x.IsWaiting, StoreID=x.StoreID, ItemUnitID=x.ItemUnitID, Quantity=x.Quantity, LastCost=x.LastCost,
            SmallUnitPrice=x.SmallUnitPrice, UnitPrice=x.UnitPrice, TotalPrice=x.TotalPrice, VAT=x.VAT, NetUnitPrice=x.NetUnitPrice,
            NetTotalPrice=x.NetTotalPrice, VAT_Discount=x.VAT_Discount, ItemUnitType=x.ItemUnitType, IsPrint=x.IsPrint, ItemNote=x.ItemNote,
            IsPrintCook=x.IsPrintCook, UnitNumber=x.UnitNumber, Weight=x.Weight, Height=x.Height, DetailsData=x.DetailsData, width=x.width,
            Long=x.Long, Bounce=x.Bounce, Amount_Discount=x.Amount_Discount, Per_Discount=x.Per_Discount, TafqitUnitId=x.TafqitUnitId,
            TafqitQunitity=x.TafqitQunitity, TobaccoTax=x.TobaccoTax, TobaccoTaxDis=x.TobaccoTaxDis, TotalWithTaxTobacco=x.TotalWithTaxTobacco,
            PriceInstall=x.PriceInstall, PriceInstallDise=x.PriceInstallDise
        };

    private sealed class PaymentType
    {
        public int Id { get; }
        public string Name { get; }
        public PaymentType(int id, string name) { Id = id; Name = name; }
        public override string ToString() => Name;
    }
}
