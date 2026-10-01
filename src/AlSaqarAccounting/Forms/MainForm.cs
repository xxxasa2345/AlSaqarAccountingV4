using System.Collections.Generic;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class MainForm : Form
{
    private readonly AppSession _session;
    private readonly SchemaService _schema;
    private readonly StoredProcedureExecutor _sp;
    private readonly SecurityService _security;
    private readonly ScreenRouter _router;
    private readonly string _connectionString;
    private readonly MenuStrip _menu = new();
    private readonly ToolStrip _toolStrip = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly ToolStripStatusLabel _screenCount = new();
    private readonly ToolStripStatusLabel _clock = new();
    private readonly TextBox _search = new();
    private readonly Panel _home = new() { Dock = DockStyle.Fill, BackColor = Color.WhiteSmoke };
    private List<ScreenAccess> _screens = new();

    public MainForm(AppSession session, SchemaService schema, StoredProcedureExecutor sp, SecurityService security, string connectionString)
    {
        _session = session; _schema = schema; _sp = sp; _security = security; _connectionString = connectionString;
        _router = new ScreenRouter(_connectionString, session);
        Text = $"الصقر للمحاسبة ERP — {_session.UserName}";
        WindowState = FormWindowState.Maximized; MinimumSize = new Size(1200, 760); StartPosition = FormStartPosition.CenterScreen;
        IsMdiContainer = true; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; KeyPreview = true;
        BuildShell(); WireEvents(); ShowHome();
    }

    private void BuildShell()
    {
        BuildMenu(); BuildToolbar();
        _status.Text = $"المستخدم: {_session.UserName}"; _screenCount.Text = "الشاشات: ..."; _clock.Spring = true; _clock.TextAlign = ContentAlignment.MiddleLeft;
        _statusStrip.Items.Add(_status); _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "|" }); _statusStrip.Items.Add(_screenCount);
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "|" }); _statusStrip.Items.Add(new ToolStripStatusLabel($"الفرع: {_session.BranchId?.ToString() ?? "-"}"));
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "|" }); _statusStrip.Items.Add(new ToolStripStatusLabel($"المجموعة: {_session.GroupId?.ToString() ?? "-"}"));
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "|" }); _statusStrip.Items.Add(_clock);
        _home.BringToFront(); Controls.Add(_home); Controls.Add(_toolStrip); Controls.Add(_menu); Controls.Add(_statusStrip); MainMenuStrip = _menu;
    }

    private void BuildMenu()
    {
        _menu.Dock = DockStyle.Top; _menu.RightToLeft = RightToLeft.Yes; _menu.Font = new Font("Tahoma", 10);
        var home = new ToolStripMenuItem("الرئيسية"); home.Click += (_, _) => ShowHome(); _menu.Items.Add(home);
        var refresh = new ToolStripMenuItem("تحديث الصلاحيات"); refresh.Click += async (_, _) => await LoadSecurityAsync(); _menu.Items.Add(refresh);
        var modules = new ToolStripMenuItem("الوحدات") { Name = "ModulesMenu" }; _menu.Items.Add(modules);

        var system = new ToolStripMenuItem("النظام");
        system.DropDownItems.Add("اختبار الاتصال", null, async (_, _) => await CheckConnectionAsync());
        if (_session.GroupId == 1)
        {
            system.DropDownItems.Add("إدارة التراخيص", null, (_, _) => OpenLicenseManagement());
        }
        system.DropDownItems.Add(new ToolStripSeparator());
        system.DropDownItems.Add("تسجيل الخروج", null, (_, _) => Close());
        _menu.Items.Add(system);
    }

    private void OpenLicenseManagement()
    {
        try
        {
            var service = new LicenseService(new DbExecutor(new SqlConnectionFactory(_connectionString)));
            using var form = new LicenseManagementForm(_session, service);
            form.StartPosition = FormStartPosition.CenterParent;
            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "إدارة التراخيص", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BuildToolbar()
    {
        _toolStrip.Dock = DockStyle.Top; _toolStrip.GripStyle = ToolStripGripStyle.Hidden; _toolStrip.RightToLeft = RightToLeft.Yes; _toolStrip.Padding = new Padding(6, 3, 6, 3);
        var home = new ToolStripButton("الرئيسية"); home.Click += (_, _) => ShowHome();
        var refresh = new ToolStripButton("تحديث"); refresh.Click += async (_, _) => await LoadSecurityAsync();
        var searchLabel = new ToolStripLabel("بحث:"); _search.Width = 280; _search.RightToLeft = RightToLeft.Yes; _search.Margin = new Padding(8, 2, 8, 2); _search.BorderStyle = BorderStyle.FixedSingle;
        var closeCurrent = new ToolStripButton("إغلاق الشاشة"); closeCurrent.Click += (_, _) => { var active = ActiveMdiChild; if (active is not null) active.Close(); else ShowHome(); };
        _toolStrip.Items.Add(home); _toolStrip.Items.Add(new ToolStripSeparator()); _toolStrip.Items.Add(refresh); _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(searchLabel); _toolStrip.Items.Add(new ToolStripControlHost(_search)); _toolStrip.Items.Add(new ToolStripSeparator()); _toolStrip.Items.Add(closeCurrent);
    }

    private void WireEvents()
    {
        Shown += async (_, _) => await LoadSecurityAsync(); _search.TextChanged += (_, _) => RebuildModuleMenu(_search.Text);
        _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); timer.Start(); FormClosed += (_, _) => timer.Dispose();
        KeyDown += (_, e) => { if (e.KeyCode == Keys.F5) { e.Handled = true; _ = LoadSecurityAsync(); } else if (e.KeyCode == Keys.F10) { e.Handled = true; ShowHome(); } };
    }

    private async Task LoadSecurityAsync()
    {
        try
        {
            UseWaitCursor = true; _screens = (await _security.GetAccessibleScreensAsync(_session)).ToList(); var groupName = await _security.GetGroupNameAsync(_session);
            _status.Text = $"المستخدم: {_session.UserName}"; _screenCount.Text = $"الشاشات: {_screens.Count:N0}"; SetStatusToolTip(groupName); RebuildModuleMenu(_search.Text); ShowHome();
        }
        catch (Exception ex)
        {
            _screens.Clear(); _status.Text = "تعذر تحميل الصلاحيات"; _screenCount.Text = "الشاشات: 0";
            MessageBox.Show(this, "تعذر تحميل الصلاحيات:\r\n" + ex.GetBaseException().Message, "الصلاحيات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private void SetStatusToolTip(string? groupName)
    {
        _status.ToolTipText = $"المستخدم: {_session.UserName}\r\nالفرع: {_session.BranchId?.ToString() ?? "-"}\r\nالمجموعة: {groupName ?? _session.GroupId?.ToString() ?? "-"}";
    }

    private void RebuildModuleMenu(string? filter)
    {
        var modules = _menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Name == "ModulesMenu"); if (modules is null) return;
        modules.DropDownItems.Clear(); var text = filter?.Trim() ?? string.Empty;
        var groups = _screens.Where(s => string.IsNullOrWhiteSpace(text) || s.ScreenName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || s.ModuleDisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            .GroupBy(s => s.ModuleDisplayName).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);
        foreach (var group in groups)
        {
            var module = new ToolStripMenuItem(group.Key);
            foreach (var screen in group.OrderBy(s => s.ScreenNum ?? int.MaxValue).ThenBy(s => s.Id))
            {
                var item = new ToolStripMenuItem(string.IsNullOrWhiteSpace(screen.ScreenName) ? $"شاشة #{screen.Id}" : screen.ScreenName) { Tag = screen, Enabled = screen.AllowEnter };
                item.Click += ScreenMenu_Click; module.DropDownItems.Add(item);
            }
            modules.DropDownItems.Add(module);
        }
    }

    private void ScreenMenu_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item || item.Tag is not ScreenAccess access) return;
        if (!access.AllowEnter) { MessageBox.Show(this, "لا تملك صلاحية فتح هذه الشاشة.", "الصلاحيات", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        UseWaitCursor = true;
        try { if (!_router.TryOpen(this, access, out var message) && !string.IsNullOrWhiteSpace(message)) MessageBox.Show(this, message, "فتح الشاشة", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        finally { UseWaitCursor = false; }
    }

    private async Task CheckConnectionAsync()
    {
        try { var tables = await _schema.GetTablesAsync(); _status.Text = $"الاتصال بقاعدة البيانات: سليم — الجداول: {tables.Rows.Count:N0}"; }
        catch (Exception ex) { MessageBox.Show(this, ex.GetBaseException().Message, "اختبار الاتصال", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ShowHome()
    {
        foreach (var child in MdiChildren) child.Close(); _home.Visible = true; _home.BringToFront(); BuildDashboard();
    }

    private void BuildDashboard()
    {
        _home.Controls.Clear();
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), ColumnCount = 1, RowCount = 4 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 84)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "الصقر للمحاسبة ERP", TextAlign = ContentAlignment.MiddleRight, Font = new Font("Tahoma", 25, FontStyle.Bold), ForeColor = Color.FromArgb(30, 55, 85) }, 0, 0);
        outer.Controls.Add(new Label { Dock = DockStyle.Fill, Text = $"المستخدم: {_session.UserName}    |    الفرع: {_session.BranchId?.ToString() ?? "-"}    |    المجموعة: {_session.GroupId?.ToString() ?? "-"}", TextAlign = ContentAlignment.MiddleRight, Font = new Font("Tahoma", 11), ForeColor = Color.DimGray }, 0, 1);
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 6, 0, 6) };
        for (var i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cards.Controls.Add(MakeCard("الشاشات المسموح بها", _screens.Count.ToString("N0")), 0, 0); cards.Controls.Add(MakeCard("المستخدم الحالي", _session.UserName), 1, 0); cards.Controls.Add(MakeCard("الفرع", _session.BranchId?.ToString() ?? "-"), 2, 0); cards.Controls.Add(MakeCard("حالة النظام", "متصل"), 3, 0); outer.Controls.Add(cards, 0, 2);
        var quick = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };
        foreach (var name in new[] { "الأصناف", "العملاء", "الموردون", "الفواتير", "المبيعات", "المشتريات", "شجرة الحسابات", "السندات", "الفروع", "المخازن", "العقود", "عروض الأسعار" })
        {
            var screen = _screens.FirstOrDefault(s => string.Equals(s.ScreenName, name, StringComparison.OrdinalIgnoreCase)); if (screen is null || !screen.AllowEnter) continue;
            var button = new Button { Text = name, Width = 170, Height = 48, Margin = new Padding(7), Font = new Font("Tahoma", 10, FontStyle.Bold) };
            button.Click += (_, _) => { if (_router.TryOpen(this, screen, out var message) && !string.IsNullOrWhiteSpace(message)) MessageBox.Show(this, message); }; quick.Controls.Add(button);
        }
        var hint = new Label { Dock = DockStyle.Bottom, Height = 42, Text = "استخدم «الوحدات» من الشريط العلوي للوصول إلى كل الشاشات المسموح بها. يمكن البحث باسم الشاشة من شريط الأدوات.", TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.DimGray, Font = new Font("Tahoma", 10) };
        var workspace = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White }; workspace.Controls.Add(quick); workspace.Controls.Add(hint); outer.Controls.Add(workspace, 0, 3); _home.Controls.Add(outer);
    }

    private static Panel MakeCard(string title, string value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = $"{title}\r\n{value}", TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Tahoma", 11, FontStyle.Bold) });
        return panel;
    }
}
