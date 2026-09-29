using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational sales invoice entry screen (فاتورة مبيعات جديدة). Builds a
/// SalesInvoice and saves it through SalesService → the original
/// dbo.Insert_Order_Order_ALL procedure. No direct SQL in the form.
/// </summary>
public sealed class SalesEntryForm : Form
{
    private const decimal VatRate = 0.15m;

    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly SalesService _sales;
    private readonly StoresService _stores;

    private readonly ComboBox _customerCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _storeCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _paymentCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _itemCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, RightToLeft = RightToLeft.Yes };
    private readonly DateTimePicker _date = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
    private readonly TextBox _noteNum = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _note = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly NumericUpDown _quantity = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2, Value = 1 };
    private readonly NumericUpDown _price = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2 };
    private readonly NumericUpDown _discount = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2 };
    private readonly NumericUpDown _paid = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2 };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White,
        RightToLeft = RightToLeft.Yes,
        RowHeadersVisible = false,
        AllowUserToResizeRows = false
    };

    private readonly Label _totals = new()
    {
        Dock = DockStyle.Bottom,
        Height = 64,
        Font = new Font("Tahoma", 10, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(8)
    };

    private DataTable? _items;
    private DataTable? _customers;
    private readonly DataTable _lines = new();
    private string? _itemsIdColumn;
    private string? _itemsNameColumn;
    private string? _itemsPriceColumn;
    private string? _customersIdColumn;
    private string? _customersNameColumn;

    public bool Saved { get; private set; }

    public SalesEntryForm(
        AppSession session,
        ScreenAccess access,
        SalesService sales,
        StoresService stores)
    {
        _session = session;
        _access = access;
        _sales = sales;
        _stores = stores;

        Text = "الصقر للمحاسبة — فاتورة مبيعات جديدة";
        Width = 1200;
        Height = 780;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        MinimizeBox = false;
        MaximizeBox = false;

        BuildLinesTable();
        BuildLayout();
        Shown += async (_, _) => await LoadLookupsAsync();
        _quantity.ValueChanged += (_, _) => UpdateTotals();
        _discount.ValueChanged += (_, _) => UpdateTotals();
        _paid.ValueChanged += (_, _) => UpdateTotals();
    }

    private void BuildLinesTable()
    {
        _lines.Columns.Add("ItemID", typeof(int));
        _lines.Columns.Add("اسم الصنف", typeof(string));
        _lines.Columns.Add("الكمية", typeof(decimal));
        _lines.Columns.Add("سعر الوحدة", typeof(decimal));
        _lines.Columns.Add("الإجمالي", typeof(decimal));
        _lines.Columns.Add("الضريبة 15%", typeof(decimal));
        _lines.Columns.Add("الإجمالي بعد الضريبة", typeof(decimal));
        _grid.DataSource = _lines;
    }

    private void BuildLayout()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 96,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        var title = new Label
        {
            Text = "فاتورة مبيعات جديدة",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Tahoma", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString() ?? "-"} — الحفظ عبر الإجراء الأصلي Insert_Order_Order_ALL",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(info);
        header.Controls.Add(title);
        Controls.Add(header);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 108,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(8)
        };
        for (var i = 0; i < 4; i++)
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(fields, "العميل", _customerCombo, 0, 0);
        AddField(fields, "المخزن", _storeCombo, 1, 0);
        AddField(fields, "نوع الدفع", _paymentCombo, 2, 0);
        AddField(fields, "التاريخ", _date, 3, 0);
        AddField(fields, "رقم الفاتورة", _noteNum, 0, 1);
        AddField(fields, "الخصم", _discount, 1, 1);
        AddField(fields, "المدفوع", _paid, 2, 1);
        AddField(fields, "ملاحظات", _note, 3, 1);
        Controls.Add(fields);

        _paymentCombo.Items.AddRange(new object[] { "نقدي", "بنك", "آجل" });
        _paymentCombo.SelectedIndex = 0;
        _paymentCombo.SelectedIndexChanged += (_, _) => OnPaymentChanged();

        var strip = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColumnCount = 5,
            Padding = new Padding(8)
        };
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        AddField(strip, "الصنف", _itemCombo, 0, 0);
        AddField(strip, "الكمية", _quantity, 1, 0);
        AddField(strip, "سعر الوحدة", _price, 2, 0);
        var addLine = new Button
        {
            Text = "إضافة للفاتورة",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat
        };
        addLine.Click += (_, _) => AddLine();
        strip.Controls.Add(addLine, 3, 0);
        var removeLine = new Button
        {
            Text = "حذف السطر المحدد",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat
        };
        removeLine.Click += (_, _) => RemoveSelectedLine();
        strip.Controls.Add(removeLine, 4, 0);
        Controls.Add(strip);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };
        var save = new Button
        {
            Text = "حفظ الفاتورة",
            Width = 130,
            Height = 32,
            Enabled = _access.AllowSave,
            FlatStyle = FlatStyle.Flat
        };
        save.Click += async (_, _) => await SaveAsync();
        var close = new Button { Text = "إغلاق", Width = 100, Height = 32, FlatStyle = FlatStyle.Flat };
        close.Click += (_, _) => Close();
        toolbar.Controls.Add(save);
        toolbar.Controls.Add(close);
        Controls.Add(toolbar);

        Controls.Add(_grid);
        Controls.Add(_totals);
        UpdateTotals();
    }

    private static void AddField(TableLayoutPanel parent, string label, Control control, int column, int row)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Tahoma", 9, FontStyle.Bold),
            AutoSize = true
        }, 0, 0);
        host.Controls.Add(control, 0, 1);
        parent.Controls.Add(host, column, row);
    }

    private async Task LoadLookupsAsync()
    {
        try
        {
            UseWaitCursor = true;

            _paymentCombo.SelectedIndex = 0;

            var itemsTask = _sales.ListItemsAsync();
            var customersTask = _sales.ListCustomersAsync(_session.BranchId);
            var storesTask = _stores.ListAsync();

            await Task.WhenAll(itemsTask, customersTask, storesTask);

            _items = itemsTask.Result;
            _itemsIdColumn = FindColumn(_items, "ItemId", "ItemID", "ID", "SN");
            _itemsNameColumn = FindColumn(_items, "item_Name", "ItemName", "Name", "Item_Name");
            _itemsPriceColumn = FindColumn(_items, "SellPriceSmall", "SellPrice", "Price", "UnitPrice");
            if (_itemsIdColumn is null || _itemsNameColumn is null)
                throw new InvalidOperationException("تعذر التعرف على أعمدة الأصناف من الإجراء Get_All_Items.");
            BindCombo(_itemCombo, _items, _itemsIdColumn, _itemsNameColumn);
            _itemCombo.SelectedIndexChanged += (_, _) => UpdatePriceFromItem();

            _customers = customersTask.Result;
            _customersIdColumn = FindColumn(_customers, "ID", "SN", "AccountID", "AccountID2");
            _customersNameColumn = FindColumn(_customers, "Name", "CustSuppName", "Account_Name", "CustomerName");
            if (_customersIdColumn is null || _customersNameColumn is null)
                throw new InvalidOperationException("تعذر التعرف على أعمدة العملاء من الإجراء Select_AccountCustomer.");
            BindCombo(_customerCombo, _customers, _customersIdColumn, _customersNameColumn);

            var stores = storesTask.Result;
            var storeId = FindColumn(stores, "ID", "SN", "StoreID");
            var storeName = FindColumn(stores, "Store_Name", "Name", "StoreName");
            if (storeId is not null && storeName is not null)
                BindCombo(_storeCombo, stores, storeId, storeName);
            else
                _storeCombo.Items.Add("غير محدد");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر تحميل البيانات: " + ex.GetBaseException().Message,
                "فاتورة مبيعات", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static void BindCombo(ComboBox combo, DataTable table, string idColumn, string nameColumn)
    {
        combo.DataSource = null;
        combo.DataSource = new BindingSource(table, null);
        combo.DisplayMember = nameColumn;
        combo.ValueMember = idColumn;

        var names = new AutoCompleteStringCollection();
        foreach (DataRow row in table.Rows)
        {
            var name = row[nameColumn]?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
        combo.AutoCompleteSource = AutoCompleteSource.CustomSource;
        combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        combo.AutoCompleteCustomSource = names;
    }

    private static string? FindColumn(DataTable table, params string[] names)
    {
        foreach (DataColumn column in table.Columns)
            foreach (var name in names)
                if (string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                    return column.ColumnName;
        return null;
    }

    private void UpdatePriceFromItem()
    {
        if (_items is null || _itemsPriceColumn is null)
            return;
        if (!int.TryParse(_itemCombo.SelectedValue?.ToString(), out var itemId) || itemId <= 0)
            return;

        foreach (DataRow row in _items.Rows)
        {
            if (Convert.ToString(row[_itemsIdColumn]) == itemId.ToString() &&
                row[_itemsPriceColumn] is not DBNull)
            {
                _price.Value = Convert.ToDecimal(row[_itemsPriceColumn]);
                return;
            }
        }
    }

    private void AddLine()
    {
        if (_items is null || !int.TryParse(_itemCombo.SelectedValue?.ToString(), out var itemId) || itemId <= 0)
        {
            MessageBox.Show(this, "اختر صنفاً صحيحاً أولاً.", "إضافة سطر",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var quantity = _quantity.Value;
        var price = _price.Value;
        if (quantity <= 0)
        {
            MessageBox.Show(this, "الكمية يجب أن تكون أكبر من صفر.", "إضافة سطر",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var name = _itemCombo.Text.Trim();
        var total = decimal.Round(quantity * price, 2);
        var vat = decimal.Round(total * VatRate, 2);
        _lines.Rows.Add(itemId, name, quantity, price, total, vat, total + vat);
        UpdateTotals();
        _quantity.Value = 1;
        _price.Value = 0;
        _itemCombo.Focus();
    }

    private void RemoveSelectedLine()
    {
        if (_grid.CurrentRow is null) return;
        _lines.Rows[_grid.CurrentRow.Index].Delete();
        UpdateTotals();
    }

    private decimal Subtotal() => _lines.Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["الإجمالي"]));

    private void UpdateTotals()
    {
        var subtotal = Subtotal();
        var vat = decimal.Round(subtotal * VatRate, 2);
        var discount = _discount.Value;
        var net = subtotal - discount + vat;
        var paid = _paid.Value;
        _totals.Text =
            $"الإجمالي قبل الضريبة: {subtotal:N2}    |    الضريبة: {vat:N2}    |    " +
            $"الصافي النهائي: {net:N2}    |    المدفوع: {paid:N2}    |    الباقي: {net - paid:N2}";
        if (_paid.Value > net)
            _paid.Value = net;
    }

    private void OnPaymentChanged()
    {
        // آجل → المدفوع صفر افتراضياً؛ نقدي/بنك → المدفوع كامل افتراضياً.
        if (_paymentCombo.SelectedIndex == 2)
            _paid.Value = 0;
        else
            _paid.Value = decimal.Round(Subtotal() - _discount.Value + decimal.Round(Subtotal() * VatRate, 2), 2);
    }

    private async Task SaveAsync()
    {
        try
        {
            UseWaitCursor = true;

            if (_lines.Rows.Count == 0)
                throw new ArgumentException("أضف صنفاً واحداً على الأقل إلى الفاتورة.");
            if (!int.TryParse(_customerCombo.SelectedValue?.ToString(), out var customerId) || customerId <= 0)
                throw new ArgumentException("اختر العميل.");

            var invoice = new SalesInvoice
            {
                InvoiceDate = _date.Value,
                PaymentType = _paymentCombo.SelectedIndex + 1,
                CustomerId = customerId,
                CustomerName = _customerCombo.Text.Trim(),
                Note = _note.Text,
                NoteNum = _noteNum.Text,
                DiscountAmount = _discount.Value,
                AmountPaid = _paid.Value,
                StoreId = TryComboId(_storeCombo),
                SalesMan = null
            };

            foreach (DataRow line in _lines.Rows)
            {
                invoice.Lines.Add(new Order_OrdersDetails
                {
                    SN = 0,
                    ItemID = Convert.ToInt32(line["ItemID"]),
                    BranchID = _session.BranchId,
                    StoreID = invoice.StoreId,
                    Quantity = Convert.ToDecimal(line["الكمية"]),
                    UnitPrice = Convert.ToDecimal(line["سعر الوحدة"]),
                    TotalPrice = Convert.ToDecimal(line["الإجمالي"]),
                    VAT = Convert.ToDecimal(line["الضريبة 15%"]),
                    NetUnitPrice = Convert.ToDecimal(line["سعر الوحدة"]),
                    NetTotalPrice = Convert.ToDecimal(line["الإجمالي"]),
                    VAT_Discount = 0,
                    IsPrint = false,
                    IsWaiting = false
                });
            }

            await _sales.CreateAsync(invoice, _session);
            Saved = true;
            MessageBox.Show(this, "تم حفظ فاتورة المبيعات بنجاح.", "حفظ الفاتورة",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حفظ الفاتورة:\r\n" + ex.GetBaseException().Message,
                "حفظ الفاتورة", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static int? TryComboId(ComboBox combo)
    {
        var raw = combo.SelectedValue?.ToString();
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }
}
