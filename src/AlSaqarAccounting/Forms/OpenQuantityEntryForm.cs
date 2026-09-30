using System.ComponentModel;
using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class OpenQuantityEntryForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly OpenQuantityService _service;
    private readonly int? _editId;

    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill };
    private readonly TextBox _noteNum = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly NumericUpDown _itemId = new() { Minimum = 1, Maximum = 999999999, Dock = DockStyle.Fill };
    private readonly NumericUpDown _storeId = new() { Minimum = 1, Maximum = 999999999, Dock = DockStyle.Fill };
    private readonly NumericUpDown _unitId = new() { Minimum = 0, Maximum = 999999999, Dock = DockStyle.Fill };
    private readonly NumericUpDown _quantity = new() { Minimum = 0.0001m, Maximum = 999999999, DecimalPlaces = 3, Increment = 1m, Dock = DockStyle.Fill };
    private readonly NumericUpDown _unitPrice = new() { Minimum = 0, Maximum = 999999999, DecimalPlaces = 3, Dock = DockStyle.Fill };
    private readonly NumericUpDown _sellPrice = new() { Minimum = 0, Maximum = 999999999, DecimalPlaces = 3, Dock = DockStyle.Fill };
    private readonly TextBox _unitType = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoGenerateColumns = false,
        RightToLeft = RightToLeft.Yes,
        BackgroundColor = Color.White,
        RowHeadersVisible = false
    };
    private readonly Label _total = new()
    {
        Text = "الإجمالي: 0.000",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Font = new Font("Tahoma", 11, FontStyle.Bold)
    };

    private readonly BindingList<Order_OpenQuantityDetails> _lines = new();

    public bool Saved { get; private set; }

    public OpenQuantityEntryForm(
        AppSession session,
        ScreenAccess access,
        OpenQuantityService service,
        int? editId = null)
    {
        _session = session;
        _access = access;
        _service = service;
        _editId = editId;

        Text = editId.HasValue ? "تعديل الكمية الافتتاحية" : "كمية افتتاحية جديدة";
        Width = 1100;
        Height = 720;
        MinimumSize = new Size(950, 620);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        Build();
        Wire();
    }

    private void Build()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 125,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(10)
        };
        for (var i = 0; i < 4; i++)
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(header, "التاريخ", _date, 0, 0);
        AddField(header, "رقم المذكرة", _noteNum, 1, 0);
        AddField(header, "رقم الصنف", _itemId, 2, 0);
        AddField(header, "رقم المخزن", _storeId, 3, 0);
        AddField(header, "رقم الوحدة", _unitId, 0, 1);
        AddField(header, "الكمية", _quantity, 1, 1);
        AddField(header, "سعر الوحدة", _unitPrice, 2, 1);
        AddField(header, "سعر البيع", _sellPrice, 3, 1);

        var lineBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            WrapContents = false
        };
        AddButton(lineBar, "إضافة للسطر", true, AddLine);
        AddButton(lineBar, "حذف السطر", true, RemoveLine);
        lineBar.Controls.Add(_total);

        ConfigureGrid();

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        AddButton(bottom, "حفظ", _access.AllowSave || (_editId.HasValue && _access.AllowEdit), SaveAsync, 130);
        AddButton(bottom, "إلغاء", true, () => Close(), 110);

        Controls.Add(_grid);
        Controls.Add(lineBar);
        Controls.Add(header);
        Controls.Add(bottom);
    }

    private void Wire()
    {
        Shown += async (_, _) =>
        {
            try
            {
                if (_editId.HasValue)
                    await LoadExistingAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "تعذر تحميل الكمية الافتتاحية:\r\n" + ex.GetBaseException().Message,
                    "الكميات الافتتاحية",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
            }
        };
    }

    private void ConfigureGrid()
    {
        AddColumn("ItemID", "الصنف", 90);
        AddColumn("StoreID", "المخزن", 90);
        AddColumn("ItemUnitID", "الوحدة", 90);
        AddColumn("Quantity", "الكمية", 100, "N3");
        AddColumn("UnitPrice", "سعر الوحدة", 110, "N3");
        AddColumn("SellPrice", "سعر البيع", 110, "N3");
        AddColumn("TotalPrice", "الإجمالي", 120, "N3");
        AddColumn("ItemUnitType", "نوع الوحدة", 120);
        _grid.DataSource = _lines;
    }

    private void AddColumn(string property, string header, int width, string? format = null)
    {
        var c = new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = header,
            Width = width
        };
        if (format is not null)
            c.DefaultCellStyle.Format = format;
        _grid.Columns.Add(c);
    }

    private static void AddField(TableLayoutPanel panel, string labelText, Control editor, int col, int row)
    {
        var box = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight
        };
        box.Controls.Add(editor);
        box.Controls.Add(label);
        panel.Controls.Add(box, col, row);
    }

    private static void AddButton(
        Control parent,
        string text,
        bool enabled,
        Action action,
        int width = 115)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4)
        };
        b.Click += (_, _) => action();
        ((FlowLayoutPanel)parent).Controls.Add(b);
    }

    private static void AddButton(
        Control parent,
        string text,
        bool enabled,
        Func<Task> action,
        int width = 115)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4)
        };
        b.Click += async (_, _) => await action();
        ((FlowLayoutPanel)parent).Controls.Add(b);
    }

    private void AddLine()
    {
        var item = (int)_itemId.Value;
        var store = (int)_storeId.Value;
        var quantity = _quantity.Value;
        var unitPrice = _unitPrice.Value;
        var sellPrice = _sellPrice.Value;

        if (item <= 0 || store <= 0 || quantity <= 0)
        {
            MessageBox.Show(this, "أدخل الصنف والمخزن والكمية.", "إضافة سطر",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var line = new Order_OpenQuantityDetails
        {
            SN = _lines.Count + 1,
            ItemID = item,
            StoreID = store,
            ItemUnitID = _unitId.Value <= 0 ? null : (int)_unitId.Value,
            Quantity = quantity,
            UnitPrice = unitPrice,
            SellPrice = sellPrice,
            TotalPrice = quantity * unitPrice,
            ItemUnitType = string.IsNullOrWhiteSpace(_unitType.Text) ? null : _unitType.Text.Trim(),
            UnitNumber = 1m,
            BranchID = _session.BranchId
        };
        _lines.Add(line);
        Recalculate();
        _itemId.Focus();
    }

    private void RemoveLine()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Order_OpenQuantityDetails line)
            return;

        _lines.Remove(line);
        Renumber();
        Recalculate();
    }

    private void Renumber()
    {
        for (var i = 0; i < _lines.Count; i++)
            _lines[i].SN = i + 1;
        _grid.Refresh();
    }

    private void Recalculate() => _total.Text = $"الإجمالي: {_lines.Sum(x => x.TotalPrice ?? 0m):N3}";

    private async Task LoadExistingAsync()
    {
        var data = await _service.ListDetailsAsync(_editId!.Value, _session.BranchId);
        _lines.Clear();

        foreach (DataRow r in data.Rows)
        {
            _lines.Add(new Order_OpenQuantityDetails
            {
                SN = Convert.ToInt32(r["SN"]),
                Purchese_ID = NullableInt(r["Purchese_ID"]),
                ItemID = NullableInt(r["ItemID"]),
                BranchID = NullableInt(r["BranchID"]),
                StoreID = NullableInt(r["StoreID"]),
                ItemUnitID = NullableInt(r["ItemUnitID"]),
                Quantity = NullableDecimal(r["Quantity"]),
                UnitPrice = NullableDecimal(r["UnitPrice"]),
                TotalPrice = NullableDecimal(r["TotalPrice"]),
                ItemUnitType = r["ItemUnitType"] == DBNull.Value
                    ? null
                    : Convert.ToString(r["ItemUnitType"]),
                UnitNumber = NullableDecimal(r["UnitNumber"]),
                SellPrice = NullableDecimal(r["SellPrice"])
            });
        }

        Recalculate();
    }

    private static int? NullableInt(object value)
        => value == DBNull.Value || value is null ? null : Convert.ToInt32(value);

    private static decimal? NullableDecimal(object value)
        => value == DBNull.Value || value is null ? null : Convert.ToDecimal(value);

    private async Task SaveAsync()
    {
        try
        {
            UseWaitCursor = true;

            var document = new OpenQuantityDocument
            {
                DocumentDate = _date.Value.Date,
                NoteNum = string.IsNullOrWhiteSpace(_noteNum.Text) ? null : _noteNum.Text.Trim()
            };
            foreach (var line in _lines)
            {
                line.Purchese_ID = _editId;
                line.BranchID = _session.BranchId;
                document.Lines.Add(line);
            }

            if (_editId.HasValue)
                await _service.UpdateAsync(_editId.Value, document, _session);
            else
                await _service.CreateAsync(document, _session);

            Saved = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "تعذر حفظ الكمية الافتتاحية:\r\n" + ex.GetBaseException().Message,
                "الكميات الافتتاحية",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }
}
