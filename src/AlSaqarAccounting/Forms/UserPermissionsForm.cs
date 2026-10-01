using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.UI;
using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete administrative form for dbo.User_Permission.
/// </summary>
public sealed class UserPermissionsForm : Form
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

    private readonly ComboBox _group = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        RightToLeft = RightToLeft.Yes
    };

    private readonly ComboBox _screen = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        RightToLeft = RightToLeft.Yes
    };

    private readonly CheckBox _allowBranch = new() { Text = "تبديل الفرع" };
    private readonly CheckBox _allowEnter = new() { Text = "فتح" };
    private readonly CheckBox _allowSave = new() { Text = "حفظ" };
    private readonly CheckBox _allowEdit = new() { Text = "تعديل" };
    private readonly CheckBox _allowDelete = new() { Text = "حذف" };
    private readonly CheckBox _allowPrint = new() { Text = "طباعة" };
    private readonly CheckBox _allowExport = new() { Text = "تصدير" };
    private int? _editingId;

    public UserPermissionsForm(AppSession session, ScreenAccess access, SecurityAdministrationService service)
    {
        _session = session;
        _access = access;
        _service = service;
        Text = "صلاحيات الشاشات — dbo.User_Permission";
        Width = 1300;
        Height = 820;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildUi();
        Shown += async (_, _) => await LoadAllAsync();
    }

    private void BuildUi()
    {
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 210,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(10)
        };
        for (var i = 0; i < 4; i++)
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(editor, "مجموعة المستخدمين", _group, 0, 0);
        AddField(editor, "الشاشة", _screen, 1, 0);

        var flags = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true
        };
        flags.Controls.AddRange(new Control[]
        {
            _allowBranch, _allowEnter, _allowSave, _allowEdit,
            _allowDelete, _allowPrint, _allowExport
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
        var add = new Button { Text = "صلاحية جديدة", Width = 120 };
        add.Click += (_, _) => ClearEditor();
        var delete = new Button { Text = "حذف", Width = 100, Enabled = _access.AllowDelete };
        delete.Click += async (_, _) => await DeleteAsync();
        var refresh = new Button { Text = "تحديث", Width = 100 };
        refresh.Click += async (_, _) => await LoadAllAsync();
        buttons.Controls.AddRange(new Control[] { save, add, delete, refresh });
        editor.Controls.Add(buttons, 3, 1);

        Controls.Add(_grid);
        Controls.Add(editor);
        _grid.SelectionChanged += (_, _) => LoadSelected();
    }

    private static void AddField(TableLayoutPanel parent, string label, Control control, int column, int row)
    {
        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(control);
        control.Dock = DockStyle.Bottom;
        control.Height = 27;
        host.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight
        });
        parent.Controls.Add(host, column, row);
    }

    private async Task LoadAllAsync()
    {
        try
        {
            UseWaitCursor = true;

            var groupsTask = _service.GetGroupsAsync();
            var screensTask = _service.GetScreensAsync();
            var permissionsTask = _service.GetPermissionsAsync();
            await Task.WhenAll(groupsTask, screensTask, permissionsTask);

            ConfigureCombo(_group, groupsTask.Result, "ID", "Name");
            ConfigureCombo(_screen, screensTask.Result, "ID", "Screen_Name");
            _grid.DataSource = permissionsTask.Result;
            FormatGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر تحميل الصلاحيات:\r\n" + ex.GetBaseException().Message,
                "صلاحيات الشاشات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private static void ConfigureCombo(ComboBox combo, DataTable table, string valueMember, string displayMember)
    {
        combo.DataSource = null;
        combo.DataSource = new BindingSource(table, null);
        combo.ValueMember = valueMember;
        combo.DisplayMember = displayMember;
        combo.SelectedIndex = table.Rows.Count > 0 ? 0 : -1;
    }

    private void LoadSelected()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row)
            return;

        _editingId = Convert.ToInt32(row["ID"]);
        SelectComboValue(_group, row["GroupID"]);
        SelectComboValue(_screen, row["ScreenID"]);
        _allowBranch.Checked = ToBool(row["Allow_Branch"]);
        _allowEnter.Checked = ToBool(row["Allow_Enter"]);
        _allowSave.Checked = ToBool(row["Allow_Save"]);
        _allowEdit.Checked = ToBool(row["Allow_Edit"]);
        _allowDelete.Checked = ToBool(row["Allow_Delete"]);
        _allowPrint.Checked = ToBool(row["Allow_Print"]);
        _allowExport.Checked = ToBool(row["Allow_Export"]);
    }

    private static void SelectComboValue(ComboBox combo, object value)
    {
        if (value is null || value == DBNull.Value)
            return;
        try { combo.SelectedValue = Convert.ToInt32(value); }
        catch { }
    }

    private async Task SaveAsync()
    {
        try
        {
            if ((_editingId.HasValue && !_access.AllowEdit) || (!_editingId.HasValue && !_access.AllowSave))
                throw new UnauthorizedAccessException("لا تملك صلاحية حفظ/تعديل صلاحيات الشاشات.");
            if (!TryComboInt(_group, out var groupId))
                throw new InvalidOperationException("اختر مجموعة المستخدمين.");
            if (!TryComboInt(_screen, out var screenId))
                throw new InvalidOperationException("اختر الشاشة.");

            await _service.SavePermissionAsync(
                _editingId,
                groupId,
                screenId,
                _allowBranch.Checked,
                _allowEnter.Checked,
                _allowSave.Checked,
                _allowEdit.Checked,
                _allowDelete.Checked,
                _allowPrint.Checked,
                _allowExport.Checked,
                _session);

            ClearEditor();
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حفظ الصلاحية:\r\n" + ex.GetBaseException().Message,
                "صلاحيات الشاشات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete)
        {
            MessageBox.Show(this, "لا تملك صلاحية حذف صلاحيات الشاشات.", "الصلاحيات", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_editingId.HasValue)
            return;

        if (MessageBox.Show(this, "حذف الصلاحية المحددة؟", "تأكيد الحذف",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            await _service.DeletePermissionAsync(_editingId.Value);
            ClearEditor();
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف الصلاحية:\r\n" + ex.GetBaseException().Message,
                "صلاحيات الشاشات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearEditor()
    {
        _editingId = null;
        if (_group.Items.Count > 0) _group.SelectedIndex = 0;
        if (_screen.Items.Count > 0) _screen.SelectedIndex = 0;
        _allowBranch.Checked = false;
        _allowEnter.Checked = false;
        _allowSave.Checked = false;
        _allowEdit.Checked = false;
        _allowDelete.Checked = false;
        _allowPrint.Checked = false;
        _allowExport.Checked = false;
        _grid.ClearSelection();
    }

    private void FormatGrid()
    {
        var hidden = new[] { "ID", "GroupID", "ScreenID" };
        foreach (var column in hidden)
            if (_grid.Columns.Contains(column))
                _grid.Columns[column].Visible = false;

        if (_grid.Columns.Contains("GroupName"))
            _grid.Columns["GroupName"].HeaderText = "المجموعة";
        if (_grid.Columns.Contains("ScreenName"))
            _grid.Columns["ScreenName"].HeaderText = "الشاشة";
        if (_grid.Columns.Contains("Allow_Branch"))
            _grid.Columns["Allow_Branch"].HeaderText = "فرع";
        if (_grid.Columns.Contains("Allow_Enter"))
            _grid.Columns["Allow_Enter"].HeaderText = "فتح";
        if (_grid.Columns.Contains("Allow_Save"))
            _grid.Columns["Allow_Save"].HeaderText = "حفظ";
        if (_grid.Columns.Contains("Allow_Edit"))
            _grid.Columns["Allow_Edit"].HeaderText = "تعديل";
        if (_grid.Columns.Contains("Allow_Delete"))
            _grid.Columns["Allow_Delete"].HeaderText = "حذف";
        if (_grid.Columns.Contains("Allow_Print"))
            _grid.Columns["Allow_Print"].HeaderText = "طباعة";
        if (_grid.Columns.Contains("Allow_Export"))
            _grid.Columns["Allow_Export"].HeaderText = "تصدير";
    }

    private static bool TryComboInt(ComboBox combo, out int value)
        => int.TryParse(combo.SelectedValue?.ToString(), out value) && value > 0;

    private static bool ToBool(object value)
        => value is not null && value != DBNull.Value && Convert.ToBoolean(value);
}
