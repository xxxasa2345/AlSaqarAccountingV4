using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.Forms;

public sealed class LicenseManagementForm : Form
{
    private readonly AppSession _session;
    private readonly LicenseService _service;
    private readonly TextBox _key = new();
    private readonly TextBox _company = new();
    private readonly TextBox _customer = new();
    private readonly TextBox _machine = new();
    private readonly TextBox _notes = new();
    private readonly NumericUpDown _users = new();
    private readonly DateTimePicker _expiry = new();
    private readonly CheckBox _noExpiry = new() { Text = "بدون انتهاء", AutoSize = true };
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();

    public LicenseManagementForm(AppSession session, LicenseService service)
    {
        _session = session;
        _service = service;

        Text = "إدارة تراخيص الصقر للمحاسبة";
        Width = 1180;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Tahoma", 9.5f);

        BuildUi();
        Shown += async (_, _) => await ReloadAsync();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
        for (var i = 0; i < 4; i++) editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (var i = 0; i < 5; i++) editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        _key.Dock = DockStyle.Fill;
        _company.Dock = DockStyle.Fill;
        _customer.Dock = DockStyle.Fill;
        _machine.Dock = DockStyle.Fill;
        _notes.Dock = DockStyle.Fill;
        _users.Minimum = 1; _users.Maximum = 100000; _users.Value = 1; _users.Dock = DockStyle.Left;
        _expiry.Value = DateTime.Now.Date.AddYears(1); _expiry.Dock = DockStyle.Fill;

        AddField(editor, "مفتاح الترخيص", _key, 0, 0);
        AddField(editor, "الشركة", _company, 1, 0);
        AddField(editor, "العميل", _customer, 2, 0);
        AddField(editor, "عدد المستخدمين", _users, 3, 0);
        AddField(editor, "تاريخ الانتهاء", _expiry, 0, 1);
        editor.Controls.Add(_noExpiry, 1, 1);
        AddField(editor, "بصمة الجهاز (اختياري)", _machine, 2, 1);
        AddField(editor, "ملاحظات", _notes, 3, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var generate = MakeButton("توليد مفتاح");
        generate.Click += (_, _) => _key.Text = LicenseService.GenerateKey();
        var create = MakeButton("إصدار ترخيص");
        create.Click += async (_, _) => await CreateAsync();
        var activate = MakeButton("تفعيل المفتاح");
        activate.Click += async (_, _) => await ActivateAsync();
        var refresh = MakeButton("تحديث");
        refresh.Click += async (_, _) => await ReloadAsync();
        var deactivate = MakeButton("إلغاء تنشيط المحدد");
        deactivate.Click += async (_, _) => await DeactivateAsync();
        buttons.Controls.AddRange(new Control[] { generate, create, activate, refresh, deactivate });
        editor.Controls.Add(buttons, 0, 2); editor.SetColumnSpan(buttons, 4);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RightToLeft = RightToLeft.Yes;

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.DimGray,
            Text = $"المستخدم الإداري: {_session.UserName} — الجهاز الحالي: {LicenseService.GetMachineFingerprint()}"
        };

        root.Controls.Add(editor, 0, 0);
        root.Controls.Add(_grid, 0, 1);
        root.Controls.Add(hint, 0, 2);
        Controls.Add(root);
    }

    private void AddField(TableLayoutPanel table, string caption, Control control, int column, int row)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var label = new Label { Text = caption, Dock = DockStyle.Right, Width = 125, TextAlign = ContentAlignment.MiddleRight };
        control.Location = new Point(0, 2);
        control.Width = 220;
        panel.Controls.Add(control); panel.Controls.Add(label);
        table.Controls.Add(panel, column, row);
    }

    private static Button MakeButton(string text) => new() { Text = text, AutoSize = true, Height = 32, Margin = new Padding(5) };

    private async Task ReloadAsync()
    {
        try
        {
            UseWaitCursor = true;
            var data = await _service.GetLicensesAsync();
            _grid.DataSource = data;
            _status.Text = $"عدد التراخيص: {data.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "التراخيص", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private async Task CreateAsync()
    {
        try
        {
            var key = string.IsNullOrWhiteSpace(_key.Text) ? LicenseService.GenerateKey() : _key.Text;
            _key.Text = await _service.CreateLicenseAsync(
                key,
                "Standard",
                _company.Text,
                _customer.Text,
                _noExpiry.Checked ? (DateTime?)null : _expiry.Value.Date,
                (int)_users.Value,
                string.IsNullOrWhiteSpace(_machine.Text) ? null : _machine.Text.Trim(),
                _notes.Text,
                _session.UserId);
            Clipboard.SetText(_key.Text);
            MessageBox.Show(this, "تم إصدار الترخيص ونسخ المفتاح إلى الحافظة.", "التراخيص", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await ReloadAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.GetBaseException().Message, "إصدار الترخيص", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ActivateAsync()
    {
        try
        {
            await _service.ActivateAsync(_key.Text);
            var status = await _service.ValidateInstalledAsync();
            MessageBox.Show(this, status.Message, "تفعيل الترخيص", MessageBoxButtons.OK,
                status.IsValid ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            await ReloadAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.GetBaseException().Message, "تفعيل الترخيص", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task DeactivateAsync()
    {
        if (_grid.CurrentRow == null || _grid.CurrentRow.Cells[0].Value == null) return;
        if (!Guid.TryParse(_grid.CurrentRow.Cells[0].Value.ToString(), out var id)) return;
        if (MessageBox.Show(this, "هل تريد إلغاء تنشيط الترخيص المحدد؟", "التراخيص", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { await _service.DeactivateAsync(id); await ReloadAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.GetBaseException().Message, "التراخيص", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
