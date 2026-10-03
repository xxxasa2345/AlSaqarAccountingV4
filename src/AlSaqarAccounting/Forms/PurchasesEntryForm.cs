using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational purchase invoice entry screen (فاتورة مشتريات جديدة). Builds a
/// PurchaseInvoice and saves it through PurchasesService → the original
/// dbo.Insert_Order_Purchases procedure (header + lines in a single call).
/// No direct SQL in the form.
/// </summary>
public sealed class PurchasesEntryForm : Form
{

    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly PurchasesService _purchases;
    private readonly StoresService _stores;
    private readonly CustSupService _custSup;

    private readonly ComboBox _supplierCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _storeCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _paymentCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _itemCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, RightToLeft = RightToLeft.Yes };
    private readonly DateTimePicker _date = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
    private readonly TextBox _noteNum = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _note = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly NumericUpDown _quantity = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2, Value = 1 };
    private readonly NumericUpDown _price = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2 };
    private readonly NumericUpDown _sellPrice = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2 };
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
    private bool _vatEnabled;
    private decimal _vatRate;
    private int? _defaultStoreId;
    private readonly DataTable _lines = new();
    private string? _itemsIdColumn;
    private string? _itemsNameColumn;
    private string? _itemsPriceColumn;

    public bool Saved { get; private set; }

    public PurchasesEntryForm(
        AppSession session,
        ScreenAccess access,
        PurchasesService purchases,
        StoresService stores,
        CustSupService custSup)
    {
        _session = session;
        _access = access;
        _purchases = purchases;
        _stores = stores;
        _custSup = custSup;

        ErpTheme.ApplyForm(this);
        Text = "الصقر للمحاسبة — فاتورة مشتريات جديدة";
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
        _lines.Columns.Add("سعر الشراء", typeof(decimal));
        _lines.Columns.Add("الإجمالي", typeof(decimal));
        _lines.Columns.Add("الضريبة 15%", typeof(decimal));
        _lines.Columns.Add("الإجمالي بعد الضريبة", typeof(decimal));
        _lines.Columns.Add("سعر البيع", typeof(decimal));
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
            Text = "فاتورة مشتريات جديدة",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Tahoma", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString() ?? "-"} — الحفظ عبر الإجراء الأصلي Insert_Order_Purchases",
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

        AddField(fields, "المورد", _supplierCombo, 0, 0);
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
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        AddField(strip, "الصنف", _itemCombo, 0, 0);
        AddField(strip, "الكمية", _quantity, 1, 0);
        AddField(strip, "سعر الشراء", _price, 2, 0);
        AddField(strip, "سعر البيع", _sellPrice, 3, 0);
        var addLine = new Button
        {
            Text = "إضافة للفاتورة",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat
        };
        addLine.Click += (_, _) => AddLine();
        strip.Controls.Add(addLine, 4, 0);
        Controls.Add(strip);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 7, 8, 7),
            WrapContents = false
        };

        Button Action(string text, int width, bool enabled, Action action, bool primary = false)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                Enabled = enabled,
                Margin = new Padding(4)
            };
            button.Click += (_, _) => action();
            ErpTheme.ConfigureToolbarButton(button, primary);
            toolbar.Controls.Add(button);
            return button;
        }

        Action("حفظ الفاتورة", 135, _access.AllowSave, () => _ = SaveAsync(), true);
        Action("حذف السطر", 125, true, RemoveSelectedLine);
        Action("إعادة الحساب", 120, true, UpdateTotals);
        Action("فاتورة جديدة", 120, _access.AllowSave, NewDraft);
        Action("إغلاق", 100, true, Close);

        Controls.Add(toolbar);

        Controls.Add(_grid);
        Controls.Add(_totals);
        ErpTheme.ConfigureGrid(_grid);
        ErpTheme.StyleRecursive(this);
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

            var itemsTask = _purchases.ListItemsAsync();
            var suppliersTask = _custSup.ListSuppliersAsync();
            var storesTask = _stores.ListAsync();

            await Task.WhenAll(itemsTask, suppliersTask, storesTask);

            _items = itemsTask.Result;
            var settings = await _purchases.GetEntrySettingsAsync();
            _vatEnabled = settings.VatEnabled;
            _vatRate = settings.VatRate;
            _defaultStoreId = settings.DefaultStoreId;

            _itemsIdColumn = FindColumn(_items, "ItemId", "ItemID", "ID", "SN");
            _itemsNameColumn = FindColumn(_items, "item_Name", "ItemName", "Name", "Item_Name");
            _itemsPriceColumn = FindColumn(_items, "SellPriceSmall", "SellPrice", "Price", "UnitPrice");
            if (_itemsIdColumn is null || _itemsNameColumn is null)
                throw new InvalidOperationException("تعذر التعرف على أعمدة الأصناف من الإجراء Get_All_Items.");
            BindCombo(_itemCombo, _items, _itemsIdColumn, _itemsNameColumn);
            _itemCombo.SelectedIndexChanged += (_, _) => UpdatePriceFromItem();

            var suppliers = suppliersTask.Result;
            var supplierId = FindColumn(suppliers, "ID", "SN");
            var supplierName = FindColumn(suppliers, "CustSuppName", "Name");
            if (supplierId is not null && supplierName is not null)
                BindCombo(_supplierCombo, suppliers, supplierId, supplierName);

            var stores = storesTask.Result;
            var storeId = FindColumn(stores, "ID", "SN", "StoreID");
            var storeName = FindColumn(stores, "Store_Name", "Name", "StoreName");
            if (storeId is not null && storeName is not null)
            {
                BindCombo(_storeCombo, stores, storeId, storeName);
                if (_defaultStoreId.HasValue)
                {
                    try { _storeCombo.SelectedValue = _defaultStoreId.Value; }
                    catch { }
                }
            }
            else
                _storeCombo.Items.Add("غير محدد");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر تحميل البيانات: " + ex.GetBaseException().Message,
                "فاتورة مشتريات", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _sellPrice.Value = Convert.ToDecimal(row[_itemsPriceColumn]);
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
        var vat = _vatEnabled ? decimal.Round(total * _vatRate, 2) : 0m;
        _lines.Rows.Add(itemId, name, quantity, price, total, vat, total + vat, _sellPrice.Value);
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
        var vat = _vatEnabled ? decimal.Round(subtotal * _vatRate, 2) : 0m;
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
        if (_paymentCombo.SelectedIndex == 2)
            _paid.Value = 0;
        else
            _paid.Value = decimal.Round(Subtotal() - _discount.Value + (_vatEnabled ? decimal.Round(Subtotal() * _vatRate, 2) : 0m), 2);
    }

    private async Task SaveAsync()
    {
        try
        {
            UseWaitCursor = true;

            if (_lines.Rows.Count == 0)
                throw new ArgumentException("أضف صنفاً واحداً على الأقل إلى الفاتورة.");
            var supplierId = TryComboId(_supplierCombo) ?? 0;

            var invoice = new PurchaseInvoice
            {
                InvoiceDate = _date.Value,
                PaymentType = _paymentCombo.SelectedIndex + 1,
                SupplierId = supplierId,
                SupplierName = _supplierCombo.Text.Trim(),
                Note = _note.Text,
                NoteNum = _noteNum.Text,
                DiscountAmount = _discount.Value,
                AmountPaid = _paid.Value,
                StoreId = TryComboId(_storeCombo)
            };

            foreach (DataRow line in _lines.Rows)
            {
                invoice.Lines.Add(new Order_PurchasesDetails
                {
                    SN = 0,
                    ItemID = Convert.ToInt32(line["ItemID"]),
                    BranchID = _session.BranchId,
                    StoreID = invoice.StoreId,
                    Quantity = Convert.ToDecimal(line["الكمية"]),
                    UnitPrice = Convert.ToDecimal(line["سعر الشراء"]),
                    TotalPrice = Convert.ToDecimal(line["الإجمالي"]),
                    VAT = Convert.ToDecimal(line["الضريبة 15%"]),
                    NetUnitPrice = Convert.ToDecimal(line["سعر الشراء"]),
                    NetTotalPrice = Convert.ToDecimal(line["الإجمالي"]),
                    VAT_Discount = 0,
                    Bounce = 0,
                    DiscNum = 0,
                    DiscPercent = 0,
                    SellPrice = Convert.ToDecimal(line["سعر البيع"])
                });
            }

            await _purchases.CreateAsync(invoice, _session);
            Saved = true;
            MessageBox.Show(this, "تم حفظ فاتورة المشتريات بنجاح.", "حفظ الفاتورة",
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


    private void NewDraft()
    {
        _lines.Rows.Clear();
        _supplierCombo.SelectedIndex = _supplierCombo.Items.Count > 0 ? 0 : -1;
        _storeCombo.SelectedIndex = _storeCombo.Items.Count > 0 ? 0 : -1;
        _paymentCombo.SelectedIndex = 0;
        _date.Value = DateTime.Today;
        _noteNum.Clear();
        _note.Clear();
        _discount.Value = 0;
        _paid.Value = 0;
        _itemCombo.Focus();
        UpdateTotals();
    }

    private static int? TryComboId(ComboBox combo)
    {
        var raw = combo.SelectedValue?.ToString();
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }
}
