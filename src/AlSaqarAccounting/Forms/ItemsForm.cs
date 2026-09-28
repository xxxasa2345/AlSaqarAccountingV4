using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete inventory item screen. It is backed by GTSdb2026 procedures,
/// not the generic OperationalDataScreen.
/// </summary>
public sealed class ItemsForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly ItemsService _service;
    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new();
    private readonly TextBox _code = new();
    private readonly TextBox _name = new();
    private readonly TextBox _englishName = new();
    private readonly NumericUpDown _unitSmall = new();
    private readonly NumericUpDown _unitMedium = new();
    private readonly NumericUpDown _unitLarge = new();
    private readonly NumericUpDown _sellPrice = new();
    private readonly NumericUpDown _tax = new();
    private DataTable? _items;

    public ItemsForm(AppSession session, ScreenAccess access, ItemsService service)
    {
        _session = session;
        _access = access;
        _service = service;
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = "الأصناف";
        Width = 1250;
        Height = 760;
        RightToLeft = RightToLeft.Yes;
        StartPosition = FormStartPosition.CenterParent;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };

        var refresh = new Button { Text = "تحديث", Width = 100, Height = 32 };
        refresh.Click += async (_, _) => await LoadItemsAsync();
        toolbar.Controls.Add(refresh);

        var save = new Button { Text = "حفظ الصنف", Width = 120, Height = 32, Enabled = _access.AllowSave };
        save.Click += async (_, _) => await SaveItemAsync();
        toolbar.Controls.Add(save);

        toolbar.Controls.Add(new Label { Text = "بحث:", AutoSize = true, Padding = new Padding(8, 8, 2, 0) });
        _search.Width = 260;
        _search.TextChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(_search);
        Controls.Add(toolbar);

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 170,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(8)
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(editor, "كود الصنف", _code, 0, 0);
        AddField(editor, "اسم الصنف", _name, 1, 0);
        AddField(editor, "الاسم الإنجليزي", _englishName, 2, 0);
        AddNumericField(editor, "الوحدة الصغرى", _unitSmall, 0, 1);
        AddNumericField(editor, "الوحدة المتوسطة", _unitMedium, 1, 1);
        AddNumericField(editor, "الوحدة الكبرى", _unitLarge, 2, 1);
        AddNumericField(editor, "سعر البيع", _sellPrice, 0, 2, 4);
        AddNumericField(editor, "الضريبة %", _tax, 1, 2, 2);
        Controls.Add(editor);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        Controls.Add(_grid);

        Shown += async (_, _) => await LoadItemsAsync();
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control, int column, int row)
    {
        var box = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var text = new Label { Text = label, Dock = DockStyle.Top, Height = 24 };
        control.Dock = DockStyle.Fill;
        box.Controls.Add(control);
        box.Controls.Add(text);
        panel.Controls.Add(box, column, row);
    }

    private static void AddNumericField(TableLayoutPanel panel, string label, NumericUpDown control, int column, int row, int decimals = 0)
    {
        control.DecimalPlaces = decimals;
        control.Maximum = 1000000000;
        control.Minimum = 0;
        AddField(panel, label, control, column, row);
    }

    private async Task LoadItemsAsync()
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            _items = await _service.ListAsync();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "خطأ في تحميل الأصناف", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ApplyFilter()
    {
        if (_items is null) return;
        var view = new DataView(_items);
        var term = _search.Text.Trim().Replace("'", "''");
        if (term.Length > 0)
            view.RowFilter = $"Convert(ItemId, 'System.String') LIKE '%{term}%' OR Convert(Item_code, 'System.String') LIKE '%{term}%' OR Convert(item_Name, 'System.String') LIKE '%{term}%'";
        _grid.DataSource = view;
    }

    private async Task SaveItemAsync()
    {
        try
        {
            if (!_access.AllowSave)
                throw new InvalidOperationException("لا تملك صلاحية حفظ الأصناف.");

            var item = new Item_Items
            {
                Item_code = _code.Text.Trim(),
                item_Name = _name.Text.Trim(),
                item_Name_English = _englishName.Text.Trim(),
                UnitSmall = ToNullableInt(_unitSmall.Value),
                UnitMedium = ToNullableInt(_unitMedium.Value),
                UnitLarge = ToNullableInt(_unitLarge.Value),
                SellPriceSmall = _sellPrice.Value,
                Is_Tax = _tax.Value > 0,
                Tax_Value = _tax.Value
            };

            Cursor = Cursors.WaitCursor;
            await _service.CreateAsync(item, _session);
            ClearEditor();
            await LoadItemsAsync();
            MessageBox.Show(this, "تم حفظ الصنف في قاعدة البيانات.", "الأصناف", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "تعذر حفظ الصنف", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ClearEditor()
    {
        _code.Clear();
        _name.Clear();
        _englishName.Clear();
        _unitSmall.Value = 0;
        _unitMedium.Value = 0;
        _unitLarge.Value = 0;
        _sellPrice.Value = 0;
        _tax.Value = 0;
    }

    private static int? ToNullableInt(decimal value) => value <= 0 ? null : Convert.ToInt32(value);
}
