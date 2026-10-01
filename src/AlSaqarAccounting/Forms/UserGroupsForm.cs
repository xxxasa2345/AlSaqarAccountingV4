using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete security-group form corresponding to the original
/// GTSErpSystem.Frms.Security.FrmGroups screen.
/// </summary>
public sealed class UserGroupsForm : Form
{
    private readonly AppSession _session;
    private readonly UserGroupsService _service;
    private readonly ScreenAccess _access;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = true,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RightToLeft = RightToLeft.Yes
    };

    private readonly TextBox _name = new();
    private readonly NumericUpDown _branch = new() { Minimum = 0, Maximum = 1000000 };
    private readonly NumericUpDown _selectSellPrice = new() { Minimum = 0, Maximum = 1000000 };
    private readonly CheckBox _sellPrice2 = new() { Text = "سعر 2" };
    private readonly CheckBox _sellPrice3 = new() { Text = "سعر 3" };
    private readonly CheckBox _lastPrice = new() { Text = "آخر سعر" };
    private readonly CheckBox _avergPrice = new() { Text = "متوسط السعر" };
    private readonly CheckBox _provePrice = new() { Text = "إثبات السعر" };
    private readonly CheckBox _selectQuantityPrice = new() { Text = "اختيار سعر الكمية" };
    private int? _editingId;

    public UserGroupsForm(AppSession session, ScreenAccess access, UserGroupsService service)
    {
        _session = session;
        _access = access;
        _service = service;
        Text = "مجموعات المستخدمين — قاعدة البيانات الأصلية";
        Width = 1180;
        Height = 760;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildUi();
        Shown += async (_, _) => await ReloadAsync();
    }

    private void BuildUi()
    {
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 210,
            ColumnCount = 4,
            RowCount = 4,
            Padding = new Padding(10)
        };

        for (var i = 0; i < 4; i++)
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(editor, "اسم المجموعة", _name, 0, 0);
        AddField(editor, "الفرع ID", _branch, 1, 0);
        AddField(editor, "سعر البيع المحدد", _selectSellPrice, 2, 0);

        var flags = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true
        };
        flags.Controls.AddRange(new Control[]
        {
            _sellPrice2, _sellPrice3, _lastPrice, _avergPrice,
            _provePrice, _selectQuantityPrice
        });
        editor.Controls.Add(flags, 0, 1);
        editor.SetColumnSpan(flags, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        var save = new Button { Text = "حفظ", Width = 100, Enabled = _access.AllowSave || _access.AllowEdit };
        save.Click += async (_, _) => await SaveAsync();

        var add = new Button { Text = "مجموعة جديدة", Width = 120 };
        add.Click += (_, _) => ClearEditor();

        var delete = new Button { Text = "حذف", Width = 100, Enabled = _access.AllowDelete };
        delete.Click += async (_, _) => await DeleteAsync();

        var refresh = new Button { Text = "تحديث", Width = 100 };
        refresh.Click += async (_, _) => await ReloadAsync();

        buttons.Controls.AddRange(new Control[] { save, add, delete, refresh });
        editor.Controls.Add(buttons, 3, 1);

        Controls.Add(_grid);
        Controls.Add(editor);
        _grid.SelectionChanged += (_, _) => LoadSelected();
    }

    private static void AddField(TableLayoutPanel p, string label, Control control, int x, int y)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var text = new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight
        };
        control.Dock = DockStyle.Bottom;
        control.Height = 26;
        panel.Controls.Add(control);
        panel.Controls.Add(text);
        p.Controls.Add(panel, x, y);
    }

    private async Task ReloadAsync()
    {
        try
        {
            UseWaitCursor = true;
            _grid.DataSource = await _service.GetGroupsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر تحميل مجموعات المستخدمين:\r\n" +
                ex.GetBaseException().Message, "مجموعات المستخدمين",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void LoadSelected()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row)
            return;

        _editingId = Convert.ToInt32(row["ID"]);
        _name.Text = Convert.ToString(row["Name"]) ?? string.Empty;
        _branch.Value = ToInt(row["BranchID"]);
        _selectSellPrice.Value = ToInt(row["SelectSellPrice"]);
        _sellPrice2.Checked = ToBool(row["SellPrice2"]);
        _sellPrice3.Checked = ToBool(row["SellPrice3"]);
        _lastPrice.Checked = ToBool(row["LastPrice"]);
        _avergPrice.Checked = ToBool(row["AvergPrice"]);
        _provePrice.Checked = ToBool(row["ProvePrice"]);
        _selectQuantityPrice.Checked = ToBool(row["SelectQuantityPrice"]);
    }

    private async Task SaveAsync()
    {
        try
        {
            if ((_editingId.HasValue && !_access.AllowEdit) ||
                (!_editingId.HasValue && !_access.AllowSave))
                throw new UnauthorizedAccessException("لا تملك صلاحية حفظ/تعديل مجموعات المستخدمين.");

            await _service.SaveAsync(
                _editingId,
                _name.Text,
                (int)_branch.Value == 0 ? _session.BranchId : (int)_branch.Value,
                _sellPrice2.Checked,
                _sellPrice3.Checked,
                _lastPrice.Checked,
                _avergPrice.Checked,
                _provePrice.Checked,
                (int)_selectSellPrice.Value,
                _selectQuantityPrice.Checked,
                _session);

            ClearEditor();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حفظ مجموعة المستخدمين:\r\n" +
                ex.GetBaseException().Message, "مجموعات المستخدمين",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete)
        {
            MessageBox.Show(this, "لا تملك صلاحية حذف مجموعات المستخدمين.", "الصلاحيات",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_editingId.HasValue)
            return;

        if (MessageBox.Show(this,
            "هل تريد حذف المجموعة المحددة؟",
            "تأكيد الحذف",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            await _service.DeleteAsync(_editingId.Value);
            ClearEditor();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف المجموعة:\r\n" +
                ex.GetBaseException().Message, "مجموعات المستخدمين",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearEditor()
    {
        _editingId = null;
        _name.Clear();
        _branch.Value = _session.BranchId ?? 0;
        _selectSellPrice.Value = 0;
        _sellPrice2.Checked = false;
        _sellPrice3.Checked = false;
        _lastPrice.Checked = false;
        _avergPrice.Checked = false;
        _provePrice.Checked = false;
        _selectQuantityPrice.Checked = false;
        _grid.ClearSelection();
    }

    private static int ToInt(object value)
        => value is null || value == DBNull.Value ? 0 : Convert.ToInt32(value);

    private static bool ToBool(object value)
        => value is not null && value != DBNull.Value && Convert.ToBoolean(value);
}
