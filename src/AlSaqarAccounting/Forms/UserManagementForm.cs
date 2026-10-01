using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class UserManagementForm : Form
{
    private readonly AppSession _session;
    private readonly UserManagementService _service;
    private readonly ScreenAccess _access;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = true, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly TextBox _name = new();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly NumericUpDown _branch = new() { Minimum = 0, Maximum = 1000000 };
    private readonly NumericUpDown _group = new() { Minimum = 0, Maximum = 1000000 };
    private readonly TextBox _phone = new();
    private readonly TextBox _note = new();
    private readonly CheckBox _active = new() { Text = "نشط", Checked = true, AutoSize = true };
    private int? _editingId;

    public UserManagementForm(AppSession session, ScreenAccess access, UserManagementService service)
    {
        _session = session; _access = access; _service = service;
        Text = "إدارة المستخدمين - قاعدة البيانات الأصلية";
        Width = 1100; Height = 700; StartPosition = FormStartPosition.CenterParent;
        BuildUi(); Shown += async (_, _) => await ReloadAsync();
    }

    private void BuildUi()
    {
        var editor = new TableLayoutPanel { Dock = DockStyle.Top, Height = 150, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
        for (var i = 0; i < 4; i++) editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddField(editor, "اسم المستخدم", _name, 0, 0); AddField(editor, "كلمة المرور", _password, 1, 0);
        AddField(editor, "الفرع ID", _branch, 2, 0); AddField(editor, "المجموعة ID", _group, 3, 0);
        AddField(editor, "الهاتف", _phone, 0, 1); AddField(editor, "ملاحظة", _note, 1, 1);
        editor.Controls.Add(_active, 2, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "حفظ", Width = 100, Enabled = _access.AllowSave || _access.AllowEdit }; save.Click += async (_, _) => await SaveAsync();
        var newBtn = new Button { Text = "مستخدم جديد", Width = 110 }; newBtn.Click += (_, _) => ClearEditor();
        var deactivate = new Button { Text = "تعطيل", Width = 100, Enabled = _access.AllowDelete }; deactivate.Click += async (_, _) => await DeactivateAsync();
        var refresh = new Button { Text = "تحديث", Width = 100 }; refresh.Click += async (_, _) => await ReloadAsync();
        buttons.Controls.AddRange(new Control[] { save, newBtn, deactivate, refresh }); editor.Controls.Add(buttons, 3, 1);
        Controls.Add(_grid); Controls.Add(editor);
        _grid.SelectionChanged += (_, _) => LoadSelected();
    }

    private static void AddField(TableLayoutPanel p, string label, Control c, int x, int y)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(c); c.Dock = DockStyle.Bottom; c.Height = 26;
        var l = new Label { Text = label, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight };
        panel.Controls.Add(l); p.Controls.Add(panel, x, y);
    }

    private async Task ReloadAsync()
    {
        try { _grid.DataSource = await _service.GetUsersAsync(); }
        catch (Exception ex) { MessageBox.Show(this, "تعذر تحميل المستخدمين:\r\n" + ex.GetBaseException().Message, "المستخدمون", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void LoadSelected()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        _editingId = Convert.ToInt32(row["ID"]); _name.Text = Convert.ToString(row["Name"]) ?? "";
        _branch.Value = ToInt(row["BranchID"]); _group.Value = ToInt(row["GroupID"]); _active.Checked = ToBool(row["IsActive"]);
        _phone.Text = Convert.ToString(row["Phone"]) ?? ""; _note.Text = Convert.ToString(row["Note"]) ?? ""; _password.Clear();
    }

    private async Task SaveAsync()
    {
        try
        {
            if ((_editingId.HasValue && !_access.AllowEdit) ||
                (!_editingId.HasValue && !_access.AllowSave))
                throw new UnauthorizedAccessException("لا تملك صلاحية حفظ/تعديل المستخدمين.");

            await _service.SaveAsync(_editingId, _name.Text, _password.Text,
                (int)_branch.Value == 0 ? _session.BranchId : (int)_branch.Value,
                (int)_group.Value == 0 ? _session.GroupId : (int)_group.Value,
                _active.Checked, _phone.Text, _note.Text, _session.UserId, _session.BranchId);
            ClearEditor(); await ReloadAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, "تعذر حفظ المستخدم:\r\n" + ex.GetBaseException().Message, "المستخدمون", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task DeactivateAsync()
    {
        if (!_access.AllowDelete)
        {
            MessageBox.Show(this, "لا تملك صلاحية تعطيل المستخدمين.", "الصلاحيات",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_editingId.HasValue) return;
        if (MessageBox.Show(this, "هل تريد تعطيل المستخدم المحدد؟", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { await _service.DeactivateAsync(_editingId.Value, _session.UserId, _session.BranchId); ClearEditor(); await ReloadAsync(); }
        catch (Exception ex) { MessageBox.Show(this, "تعذر تعطيل المستخدم:\r\n" + ex.GetBaseException().Message); }
    }

    private void ClearEditor() { _editingId = null; _name.Clear(); _password.Clear(); _phone.Clear(); _note.Clear(); _branch.Value = _session.BranchId ?? 0; _group.Value = _session.GroupId ?? 0; _active.Checked = true; _grid.ClearSelection(); }
    private static int ToInt(object v) => v == DBNull.Value || v is null ? 0 : Convert.ToInt32(v);
    private static bool ToBool(object v) => v != DBNull.Value && v is not null && Convert.ToBoolean(v);
}
