using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.UI;
using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete administrative form for dbo.User_Screens.
/// </summary>
public sealed class UserScreensForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly SecurityAdministrationService _service;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = true,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RightToLeft = RightToLeft.Yes,
        RowHeadersVisible = false
    };

    private readonly TextBox _name = new();
    private readonly NumericUpDown _typeId = new() { Minimum = 0, Maximum = 1000000 };
    private readonly NumericUpDown _screenNum = new() { Minimum = 0, Maximum = 1000000 };
    private readonly TextBox _typeName = new();
    private readonly CheckBox _isShow = new() { Text = "تظهر في القائمة", Checked = true, AutoSize = true };
    private int? _editingId;

    public UserScreensForm(AppSession session, ScreenAccess access, SecurityAdministrationService service)
    {
        _session = session;
        _access = access;
        _service = service;
        Text = "شاشات النظام — dbo.User_Screens";
        Width = 1200;
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
            Height = 165,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(10)
        };
        for (var i = 0; i < 4; i++)
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(editor, "اسم الشاشة", _name, 0, 0);
        AddField(editor, "ScreenTypeID", _typeId, 1, 0);
        AddField(editor, "ScreenNum", _screenNum, 2, 0);
        AddField(editor, "اسم نوع الشاشة", _typeName, 3, 0);

        editor.Controls.Add(_isShow, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "حفظ", Width = 100, Enabled = _access.AllowSave || _access.AllowEdit };
        save.Click += async (_, _) => await SaveAsync();
        var add = new Button { Text = "شاشة جديدة", Width = 120 };
        add.Click += (_, _) => ClearEditor();
        var delete = new Button { Text = "حذف", Width = 100, Enabled = _access.AllowDelete };
        delete.Click += async (_, _) => await DeleteAsync();
        var refresh = new Button { Text = "تحديث", Width = 100 };
        refresh.Click += async (_, _) => await ReloadAsync();
        buttons.Controls.AddRange(new Control[] { save, add, delete, refresh });
        editor.Controls.Add(buttons, 3, 1);
        editor.SetColumnSpan(buttons, 1);

        Controls.Add(_grid);
        Controls.Add(editor);
        _grid.SelectionChanged += (_, _) => LoadSelected();
    }

    private static void AddField(TableLayoutPanel parent, string label, Control control, int column, int row)
    {
        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(control);
        control.Dock = DockStyle.Bottom;
        control.Height = 26;
        var caption = new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight
        };
        host.Controls.Add(caption);
        parent.Controls.Add(host, column, row);
    }

    private async Task ReloadAsync()
    {
        try
        {
            UseWaitCursor = true;
            _grid.DataSource = await _service.GetScreensAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر تحميل شاشات النظام:\r\n" + ex.GetBaseException().Message,
                "شاشات النظام", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private void LoadSelected()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row)
            return;

        _editingId = Convert.ToInt32(row["ID"]);
        _name.Text = Convert.ToString(row["Screen_Name"]) ?? string.Empty;
        _typeId.Value = ToInt(row["ScreenTypeID"]);
        _screenNum.Value = ToInt(row["ScreenNum"]);
        _typeName.Text = Convert.ToString(row["ScreenTypeName"]) ?? string.Empty;
        _isShow.Checked = ToBool(row["ISShow"]);
    }

    private async Task SaveAsync()
    {
        try
        {
            if ((_editingId.HasValue && !_access.AllowEdit) || (!_editingId.HasValue && !_access.AllowSave))
                throw new UnauthorizedAccessException("لا تملك صلاحية حفظ/تعديل شاشات النظام.");
            await _service.SaveScreenAsync(
                _editingId,
                _name.Text,
                _typeId.Value == 0 ? null : (int?)_typeId.Value,
                _screenNum.Value == 0 ? null : (int?)_screenNum.Value,
                _typeName.Text,
                _isShow.Checked,
                _session);

            ClearEditor();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حفظ الشاشة:\r\n" + ex.GetBaseException().Message,
                "شاشات النظام", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete)
        {
            MessageBox.Show(this, "لا تملك صلاحية حذف شاشات النظام.", "الصلاحيات", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_editingId.HasValue)
            return;

        if (MessageBox.Show(this, "حذف الشاشة المحددة؟", "تأكيد الحذف",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            await _service.DeleteScreenAsync(_editingId.Value);
            ClearEditor();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف الشاشة:\r\n" + ex.GetBaseException().Message,
                "شاشات النظام", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearEditor()
    {
        _editingId = null;
        _name.Clear();
        _typeId.Value = 0;
        _screenNum.Value = 0;
        _typeName.Clear();
        _isShow.Checked = true;
        _grid.ClearSelection();
    }

    private static int ToInt(object value)
        => value is null || value == DBNull.Value ? 0 : Convert.ToInt32(value);

    private static bool ToBool(object value)
        => value is not null && value != DBNull.Value && Convert.ToBoolean(value);
}
