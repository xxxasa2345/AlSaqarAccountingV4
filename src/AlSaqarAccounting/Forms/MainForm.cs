using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Professional Arabic ERP shell. The services bar is driven by User_Screens
/// and keeps all navigation and permission decisions in ScreenRouter.
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppSession _session;
    private readonly SchemaService _schema;
    private readonly StoredProcedureExecutor _sp;
    private readonly SecurityService _security;
    private readonly ScreenRouter _router;
    private readonly string _connectionString;

    private readonly MenuStrip _menu = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly ToolStripStatusLabel _screenCount = new();
    private readonly ToolStripStatusLabel _clock = new();

    private readonly Panel _header = new();
    private readonly FlowLayoutPanel _servicesBar = new();
    private readonly FlowLayoutPanel _screenBar = new();
    private readonly Panel _home = new()
    {
        Dock = DockStyle.Fill,
        BackColor = ErpTheme.SurfaceSoft
    };
    private readonly TextBox _search = new();

    private List<ScreenAccess> _screens = new();
    private string _selectedService = string.Empty;

    private static readonly string[] PreferredServices =
    {
        "الرئيسية",
        "الحسابات",
        "العملاء والموردون",
        "الأصناف والمخازن",
        "المبيعات والمشتريات",
        "المخزون",
        "التصنيع",
        "العقود",
        "الإيجارات",
        "المطاعم",
        "الموارد البشرية",
        "الصيانة",
        "التقارير",
        "الأمن والصلاحيات",
        "النظام والإعدادات"
    };

    private static readonly Dictionary<string, string> ServiceIcons =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["الرئيسية"] = "⌂",
            ["الحسابات"] = "▦",
            ["العملاء والموردون"] = "◎",
            ["الأصناف والمخازن"] = "▤",
            ["المبيعات والمشتريات"] = "▣",
            ["المخزون"] = "⇄",
            ["التصنيع"] = "⚙",
            ["العقود"] = "□",
            ["الإيجارات"] = "⌂",
            ["المطاعم"] = "◈",
            ["الموارد البشرية"] = "♙",
            ["الصيانة"] = "🔧",
            ["التقارير"] = "▥",
            ["الأمن والصلاحيات"] = "⚿",
            ["النظام والإعدادات"] = "☰"
        };

    public MainForm(
        AppSession session,
        SchemaService schema,
        StoredProcedureExecutor sp,
        SecurityService security,
        string connectionString)
    {
        _session = session;
        _schema = schema;
        _sp = sp;
        _security = security;
        _connectionString = connectionString;
        _router = new ScreenRouter(_connectionString, session);

        Text = $"الصقر للمحاسبة ERP — {_session.UserName}";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1200, 760);
        StartPosition = FormStartPosition.CenterScreen;
        IsMdiContainer = true;
        KeyPreview = true;

        ErpTheme.ApplyForm(this);
        BuildShell();
        WireEvents();
        ShowHome();
    }

    private void BuildShell()
    {
        BuildHiddenMenu();
        BuildHeader();
        BuildServicesBar();
        BuildScreenBar();
        BuildStatus();

        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ErpTheme.SurfaceSoft,
            Padding = new Padding(12, 10, 12, 8)
        };

        workspace.Controls.Add(_home);

        Controls.Add(workspace);
        Controls.Add(_screenBar);
        Controls.Add(_servicesBar);
        Controls.Add(_header);
        Controls.Add(_statusStrip);

        MainMenuStrip = _menu;
    }

    private void BuildHiddenMenu()
    {
        _menu.Visible = false;
        _menu.Items.Add(new ToolStripMenuItem("النظام"));
    }

    private void BuildHeader()
    {
        _header.Dock = DockStyle.Top;
        _header.Height = 82;
        _header.BackColor = ErpTheme.Surface;
        _header.Padding = new Padding(18, 10, 18, 10);
        _header.BorderStyle = BorderStyle.FixedSingle;

        var brandPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 300,
            Padding = new Padding(4)
        };

        var brand = new Label
        {
            Text = "الصقر للمحاسبة",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Tahoma", 17f, FontStyle.Bold),
            ForeColor = ErpTheme.Accent,
            TextAlign = ContentAlignment.MiddleRight
        };

        var subtitle = new Label
        {
            Text = "نظام تخطيط وإدارة موارد المنشأة",
            Dock = DockStyle.Bottom,
            Height = 24,
            Font = new Font("Tahoma", 9f),
            ForeColor = ErpTheme.Muted,
            TextAlign = ContentAlignment.MiddleRight
        };

        brandPanel.Controls.Add(subtitle);
        brandPanel.Controls.Add(brand);

        var userPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 305,
            Padding = new Padding(6, 2, 6, 2)
        };

        var userLine = new Label
        {
            Text = $"المستخدم: {_session.UserName}",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Tahoma", 9.5f, FontStyle.Bold),
            ForeColor = ErpTheme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var branchLine = new Label
        {
            Text = $"الفرع: {_session.BranchId?.ToString() ?? "-"}    |    المجموعة: {_session.GroupId?.ToString() ?? "-"}",
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = ErpTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        userPanel.Controls.Add(branchLine);
        userPanel.Controls.Add(userLine);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(12, 6, 12, 6),
            BackColor = ErpTheme.SurfaceSoft,
            Margin = new Padding(8, 0, 8, 0)
        };

        var title = new Label
        {
            Text = "مساحة العمل",
            AutoSize = true,
            Font = new Font("Tahoma", 10f, FontStyle.Bold),
            ForeColor = ErpTheme.Muted,
            Margin = new Padding(4, 10, 18, 0)
        };

        _search.Width = 320;
        _search.Height = 28;
        _search.Font = new Font("Tahoma", 9.5f);
        _search.RightToLeft = RightToLeft.Yes;
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.Margin = new Padding(4, 4, 10, 0);

        var searchLabel = new Label
        {
            Text = "بحث سريع",
            AutoSize = true,
            Font = new Font("Tahoma", 9f, FontStyle.Bold),
            ForeColor = ErpTheme.Muted,
            Margin = new Padding(4, 10, 4, 0)
        };

        actions.Controls.Add(CreateHeaderButton("⌂  الرئيسية", (_, _) => ShowHome(), true));
        actions.Controls.Add(CreateHeaderButton("↻  تحديث", async (_, _) => await LoadSecurityAsync()));
        actions.Controls.Add(CreateHeaderButton("×  إغلاق", (_, _) => CloseCurrentScreen()));
        actions.Controls.Add(CreateHeaderButton("◉  اتصال", async (_, _) => await CheckConnectionAsync()));
        if (_session.GroupId == 1)
            actions.Controls.Add(CreateHeaderButton("⚿  التراخيص", (_, _) => OpenLicenseManagement()));
        actions.Controls.Add(CreateHeaderButton("↪  خروج", (_, _) => Close()));
        actions.Controls.Add(searchLabel);
        actions.Controls.Add(_search);
        actions.Controls.Add(title);

        _header.Controls.Add(actions);
        _header.Controls.Add(userPanel);
        _header.Controls.Add(brandPanel);
    }

    private void BuildServicesBar()
    {
        _servicesBar.Dock = DockStyle.Top;
        _servicesBar.Height = 58;
        _servicesBar.FlowDirection = FlowDirection.RightToLeft;
        _servicesBar.WrapContents = false;
        _servicesBar.AutoScroll = true;
        _servicesBar.Padding = new Padding(10, 7, 10, 7);
        _servicesBar.BackColor = ErpTheme.Navigation;
        _servicesBar.RightToLeft = RightToLeft.Yes;
        _servicesBar.BorderStyle = BorderStyle.FixedSingle;
    }

    private void BuildScreenBar()
    {
        _screenBar.Dock = DockStyle.Top;
        _screenBar.Height = 50;
        _screenBar.FlowDirection = FlowDirection.RightToLeft;
        _screenBar.WrapContents = false;
        _screenBar.AutoScroll = true;
        _screenBar.Padding = new Padding(10, 5, 10, 5);
        _screenBar.BackColor = ErpTheme.Surface;
        _screenBar.RightToLeft = RightToLeft.Yes;
        _screenBar.BorderStyle = BorderStyle.FixedSingle;
    }

    private void BuildStatus()
    {
        _statusStrip.Dock = DockStyle.Bottom;
        _status.Text = $"المستخدم: {_session.UserName}";
        _screenCount.Text = "الشاشات: ...";
        _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clock.Spring = true;
        _clock.TextAlign = ContentAlignment.MiddleLeft;

        _statusStrip.Items.Add(_status);
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "│" });
        _statusStrip.Items.Add(_screenCount);
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "│" });
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = $"الفرع: {_session.BranchId?.ToString() ?? "-"}" });
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "│" });
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = $"المجموعة: {_session.GroupId?.ToString() ?? "-"}" });
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "│" });
        _statusStrip.Items.Add(_clock);
    }

    private Button CreateHeaderButton(string text, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = primary ? 120 : 105,
            Height = 34,
            Margin = new Padding(4),
            Font = new Font("Tahoma", 8.5f, FontStyle.Bold)
        };

        ErpTheme.ConfigureToolbarButton(button, primary);
        button.Click += click;
        return button;
    }

    private void RebuildServicesBar()
    {
        _servicesBar.Controls.Clear();

        var available = _screens
            .Where(s => s.AllowEnter)
            .Select(s => s.ModuleDisplayName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var services = PreferredServices
            .Where(name => name == "الرئيسية" || available.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var extra in available.Where(x => !services.Any(s => string.Equals(s, x, StringComparison.OrdinalIgnoreCase)))
                              .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
        {
            services.Add(extra);
        }

        foreach (var service in services)
        {
            var active = string.Equals(service, _selectedService, StringComparison.OrdinalIgnoreCase);

            var button = new Button
            {
                Text = $"{GetServiceIcon(service)}  {service}",
                Tag = service,
                Width = Math.Max(118, Math.Min(180, 34 + (service.Length * 8))),
                Height = 40,
                Margin = new Padding(4, 2, 4, 2),
                Font = new Font("Tahoma", 9f, FontStyle.Bold)
            };

            ConfigureServiceButton(button, active);
            button.Click += (_, _) =>
            {
                _selectedService = service;
                if (service == "الرئيسية")
                {
                    ShowHome();
                }
                else
                {
                    RebuildScreenBar();
                    HideHomeForService();
                }

                RebuildServicesBar();
            };

            _servicesBar.Controls.Add(button);
        }
    }

    private void ConfigureServiceButton(Button button, bool active)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = active ? ErpTheme.Accent : Color.FromArgb(70, 88, 108);
        button.BackColor = active ? ErpTheme.Accent : ErpTheme.Navigation;
        button.ForeColor = Color.White;
        button.Cursor = Cursors.Hand;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Padding = new Padding(8, 0, 8, 0);
    }

    private string GetServiceIcon(string service)
        => ServiceIcons.TryGetValue(service, out var icon) ? icon : "•";

    private void RebuildScreenBar()
    {
        _screenBar.Controls.Clear();

        if (string.IsNullOrWhiteSpace(_selectedService) || _selectedService == "الرئيسية")
            return;

        var filter = _search.Text.Trim();

        var screens = _screens
            .Where(s => s.AllowEnter)
            .Where(s => string.Equals(s.ModuleDisplayName, _selectedService, StringComparison.OrdinalIgnoreCase))
            .Where(s => string.IsNullOrWhiteSpace(filter) ||
                        s.ScreenName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.ModuleDisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(s => s.ScreenNum ?? int.MaxValue)
            .ThenBy(s => s.Id)
            .ToList();

        foreach (var screen in screens)
        {
            var name = string.IsNullOrWhiteSpace(screen.ScreenName)
                ? $"شاشة #{screen.Id}"
                : screen.ScreenName;

            var button = new Button
            {
                Text = name,
                Tag = screen,
                AutoSize = false,
                Width = Math.Max(130, Math.Min(230, 36 + (name.Length * 9))),
                Height = 34,
                Margin = new Padding(4, 2, 4, 2),
                Font = new Font("Tahoma", 8.5f, FontStyle.Bold)
            };

            ErpTheme.ConfigureToolbarButton(button, false);
            button.Click += (_, _) => OpenAccessScreen(screen);
            _screenBar.Controls.Add(button);
        }

        if (screens.Count == 0)
        {
            _screenBar.Controls.Add(new Label
            {
                Text = "لا توجد شاشة متاحة لهذه الخدمة.",
                AutoSize = true,
                ForeColor = ErpTheme.Muted,
                Font = new Font("Tahoma", 9f),
                Padding = new Padding(8, 9, 8, 0)
            });
        }
    }

    private void HideHomeForService()
    {
        _home.Visible = false;
        _home.SendToBack();
    }

    private void WireEvents()
    {
        Shown += async (_, _) => await LoadSecurityAsync();

        _search.TextChanged += (_, _) =>
        {
            if (_selectedService == "الرئيسية")
                BuildDashboard();
            else
                RebuildScreenBar();
        };

        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        timer.Start();

        FormClosed += (_, _) => timer.Dispose();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                _ = LoadSecurityAsync();
            }
            else if (e.KeyCode == Keys.F10)
            {
                e.Handled = true;
                ShowHome();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                CloseCurrentScreen();
            }
        };
    }

    private async Task LoadSecurityAsync()
    {
        try
        {
            UseWaitCursor = true;
            _screens = (await _security.GetAccessibleScreensAsync(_session)).ToList();
            var groupName = await _security.GetGroupNameAsync(_session);

            _status.Text = $"المستخدم: {_session.UserName}";
            _screenCount.Text = $"الشاشات: {_screens.Count:N0}";
            _status.ToolTipText =
                $"المستخدم: {_session.UserName}\r\n" +
                $"الفرع: {_session.BranchId?.ToString() ?? "-"}\r\n" +
                $"المجموعة: {groupName ?? _session.GroupId?.ToString() ?? "-"}";

            if (string.IsNullOrWhiteSpace(_selectedService))
                _selectedService = "الرئيسية";

            RebuildServicesBar();
            RebuildScreenBar();
            BuildDashboard();
        }
        catch (Exception ex)
        {
            _screens.Clear();
            _screenCount.Text = "الشاشات: 0";
            MessageBox.Show(
                this,
                "تعذر تحميل الصلاحيات:\r\n" + ex.GetBaseException().Message,
                "الصلاحيات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void OpenAccessScreen(ScreenAccess access)
    {
        if (!access.AllowEnter)
        {
            MessageBox.Show(this, "لا تملك صلاحية فتح هذه الشاشة.", "الصلاحيات",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        UseWaitCursor = true;
        try
        {
            _router.TryOpen(this, access, out var message);
            if (!string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show(this, message, "فتح الشاشة",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void CloseCurrentScreen()
    {
        var active = ActiveMdiChild;
        if (active is not null)
        {
            active.Close();
            return;
        }

        ShowHome();
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            var tables = await _schema.GetTablesAsync();
            _status.Text = $"الاتصال سليم — الجداول: {tables.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "اختبار الاتصال",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowHome()
    {
        _selectedService = "الرئيسية";
        foreach (var child in MdiChildren)
            child.Close();

        _home.Visible = true;
        _home.BringToFront();
        RebuildServicesBar();
        RebuildScreenBar();
        BuildDashboard();
    }

    private void BuildDashboard()
    {
        _home.Controls.Clear();

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18)
        };

        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "لوحة التحكم الرئيسية",
            Dock = DockStyle.Fill,
            Font = new Font("Tahoma", 23f, FontStyle.Bold),
            ForeColor = ErpTheme.Text,
            TextAlign = ContentAlignment.MiddleRight
        };

        var subtitle = new Label
        {
            Text = "إدارة الحسابات والمخزون والمبيعات والمشتريات من واجهة تشغيل موحدة",
            Dock = DockStyle.Fill,
            Font = new Font("Tahoma", 10f),
            ForeColor = ErpTheme.Muted,
            TextAlign = ContentAlignment.MiddleRight
        };

        outer.Controls.Add(title, 0, 0);
        outer.Controls.Add(subtitle, 0, 1);

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };

        for (var i = 0; i < 4; i++)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        var moduleCount = _screens
            .Select(s => s.ModuleDisplayName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Count();

        cards.Controls.Add(ErpTheme.CreateCard("الشاشات المسموح بها", _screens.Count.ToString("N0"), "الصلاحيات الحالية"), 0, 0);
        cards.Controls.Add(ErpTheme.CreateCard("الخدمات", moduleCount.ToString("N0"), "الوحدات المتاحة"), 1, 0);
        cards.Controls.Add(ErpTheme.CreateCard("الفرع", _session.BranchId?.ToString() ?? "-", "الفرع الحالي"), 2, 0);
        cards.Controls.Add(ErpTheme.CreateCard("الحالة", "متصل", "قاعدة GTSdb2026"), 3, 0);

        outer.Controls.Add(cards, 0, 2);

        var quickTitle = new Label
        {
            Text = "الاختصارات التشغيلية",
            Dock = DockStyle.Fill,
            Font = new Font("Tahoma", 12f, FontStyle.Bold),
            ForeColor = ErpTheme.Text,
            TextAlign = ContentAlignment.MiddleRight
        };

        outer.Controls.Add(quickTitle, 0, 3);

        var quick = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(4),
            BackColor = ErpTheme.Surface
        };

        var quickNames = new[]
        {
            "الأصناف", "العملاء", "الموردون", "الفواتير", "المبيعات",
            "المشتريات", "شجرة الحسابات", "السندات", "الفروع", "المخازن",
            "العقود", "عروض الأسعار", "الجرد", "الكاشير"
        };

        foreach (var name in quickNames)
        {
            var screen = _screens.FirstOrDefault(s =>
                string.Equals(s.ScreenName, name, StringComparison.OrdinalIgnoreCase));

            if (screen is null || !screen.AllowEnter)
                continue;

            var button = new Button
            {
                Text = name,
                Width = 155,
                Height = 44,
                Margin = new Padding(5),
                Font = new Font("Tahoma", 9f, FontStyle.Bold)
            };

            ErpTheme.ConfigureToolbarButton(button, true);
            button.Click += (_, _) => OpenAccessScreen(screen);
            quick.Controls.Add(button);
        }

        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = ErpTheme.Surface,
            Padding = new Padding(10)
        };

        workspace.Controls.Add(quick);
        outer.Controls.Add(workspace, 0, 4);
        _home.Controls.Add(outer);
    }

    private void OpenLicenseManagement()
    {
        try
        {
            var service = new LicenseService(new DbExecutor(new SqlConnectionFactory(_connectionString)));
            using var form = new LicenseManagementForm(_session, service)
            {
                StartPosition = FormStartPosition.CenterParent
            };
            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "إدارة التراخيص",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
