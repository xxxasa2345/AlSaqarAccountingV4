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
    private int? _selectedItemId;

    public ItemsForm(AppSession session, ScreenAccess access, ItemsService service)
    {
        _session = session;
        _access = access;
        _service = service;
        InitializeUi();
    }

    private void InitializeUi()
    {
        ErpTheme.ApplyForm(this);
        Text = "الأصناف";
        Width = 1280;
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

        AddToolbarButton(toolbar, "جديد", _access.AllowSave, NewItem, true);
        AddToolbarButton(toolbar, "حفظ الصنف", _access.AllowSave, SaveItemAsync);
        AddToolbarButton(toolbar, "تعديل", _access.AllowEdit, EditItemAsync);
        AddToolbarButton(toolbar, "حذف", _access.AllowDelete, DeleteItemAsync);
        AddToolbarButton(toolbar, "تحديث", _access.AllowEnter, LoadItemsAsync);

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
        _grid.SelectionChanged += (_, _) => LoadSelectedItem();
        ErpTheme.ConfigureGrid(_grid);
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

    private static void AddToolbarButton(
        FlowLayoutPanel toolbar,
        string text,
        bool enabled,
        Func<Task> action,
        bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = text.Length > 7 ? 120 : 95,
            Height = 34,
            Enabled = enabled,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat
        };
        button.Click += async (_, _) => await action();
        ErpTheme.ConfigureToolbarButton(button, primary);
        toolbar.Controls.Add(button);
    }

    private static void AddToolbarButton(
        FlowLayoutPanel toolbar,
        string text,
        bool enabled,
        Action action,
        bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = text.Length > 7 ? 120 : 95,
            Height = 34,
            Enabled = enabled,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat
        };
        button.Click += (_, _) => action();
        ErpTheme.ConfigureToolbarButton(button, primary);
        toolbar.Controls.Add(button);
    }

    private void NewItem()
    {
        _selectedItemId = null;
        ClearEditor();
        _grid.ClearSelection();
        _code.Focus();
    }

    private void LoadSelectedItem()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row)
            return;

        if (!int.TryParse(Convert.ToString(row.Row["ItemId"]), out var id) || id <= 0)
            return;

        _selectedItemId = id;
        _code.Text = Convert.ToString(row.Row["Item_code"]) ?? string.Empty;
        _name.Text = Convert.ToString(row.Row["item_Name"]) ?? string.Empty;
        _englishName.Text = Convert.ToString(row.Row["item_Name_English"]) ?? string.Empty;
        _unitSmall.Value = ToDecimalValue(row.Row["UnitSmall"]);
        _unitMedium.Value = ToDecimalValue(row.Row["UnitMedium"]);
        _unitLarge.Value = ToDecimalValue(row.Row["UnitLarge"]);
        _sellPrice.Value = ToDecimalValue(row.Row["SellPriceSmall"]);
        _tax.Value = ToDecimalValue(row.Row["Tax_Value"]);
    }

    private static decimal ToDecimalValue(object value)
        => value == DBNull.Value || value is null ? 0m : Math.Max(0m, Convert.ToDecimal(value));

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

            if (_selectedItemId.HasValue)
            {
                await EditItemAsync();
                return;
            }

            Cursor = Cursors.WaitCursor;
            await _service.CreateAsync(BuildEditorItem(), _session, _access.Id);
            ClearEditor();
            _selectedItemId = null;
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

    private async Task EditItemAsync()
    {
        if (!_selectedItemId.HasValue)
        {
            MessageBox.Show(this, "حدد الصنف المطلوب تعديله أولاً.", "الأصناف", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            await _service.UpdateAsync(_selectedItemId.Value, BuildEditorItem(), _session, _access.Id);
            await LoadItemsAsync();
            MessageBox.Show(this, "تم تعديل الصنف في قاعدة البيانات.", "الأصناف", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "تعذر تعديل الصنف", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private async Task DeleteItemAsync()
    {
        if (!_selectedItemId.HasValue)
        {
            MessageBox.Show(this, "حدد الصنف المطلوب حذفه أولاً.", "الأصناف", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(
                this,
                $"هل تريد حذف الصنف «{_name.Text.Trim()}»؟",
                "تأكيد حذف الصنف",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            Cursor = Cursors.WaitCursor;
            await _service.DeleteAsync(_selectedItemId.Value, _session, _access.Id);
            _selectedItemId = null;
            ClearEditor();
            await LoadItemsAsync();
            MessageBox.Show(this, "تم حذف الصنف من قاعدة البيانات.", "الأصناف", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "تعذر حذف الصنف", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private Item_Items BuildEditorItem()
        => new()
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
