using System.Collections.Generic;
using System.Linq;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Main Arabic ERP workspace. Navigation is still driven by User_Screens and
/// permissions; this class changes the workspace presentation only.
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
    private readonly ToolStrip _toolStrip = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly ToolStripStatusLabel _screenCount = new();
    private readonly ToolStripStatusLabel _clock = new();
    private readonly TextBox _search = new();

    private readonly Panel _home = new()
    {
        Dock = DockStyle.Fill,
        BackColor = ErpTheme.SurfaceSoft
    };

    private readonly Panel _navigationPanel = new()
    {
        Dock = DockStyle.Right,
        Width = 250,
        BackColor = ErpTheme.Navigation,
        Padding = new Padding(8)
    };

    private readonly FlowLayoutPanel _navigation = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        BackColor = ErpTheme.Navigation,
        RightToLeft = RightToLeft.Yes,
        Padding = new Padding(0, 4, 0, 8)
    };

    private List<ScreenAccess> _screens = new();

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
        BuildMenu();
        BuildToolbar();
        BuildStatus();

        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ErpTheme.SurfaceSoft,
            Padding = new Padding(0)
        };

        _navigationPanel.Controls.Add(_navigation);

        workspace.Controls.Add(_home);
        workspace.Controls.Add(_navigationPanel);

        Controls.Add(workspace);
        Controls.Add(_toolStrip);
        Controls.Add(_menu);
        Controls.Add(_statusStrip);
        MainMenuStrip = _menu;
    }

    private void BuildStatus()
    {
        _status.Text = $"المستخدم: {_session.UserName}";
        _screenCount.Text = "الشاشات: ...";
        _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clock.Spring = true;
        _clock.TextAlign = ContentAlignment.MiddleLeft;

        _statusStrip.Items.Add(_status);
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "|" });
        _statusStrip.Items.Add(_screenCount);
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "|" });
        _statusStrip.Items.Add(new ToolStripStatusLabel
        {
            Text = $"الفرع: {_session.BranchId?.ToString() ?? "-"}"
        });
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "|" });
        _statusStrip.Items.Add(new ToolStripStatusLabel
        {
            Text = $"المجموعة: {_session.GroupId?.ToString() ?? "-"}"
        });
        _statusStrip.Items.Add(new ToolStripStatusLabel { Text = "|" });
        _statusStrip.Items.Add(_clock);
    }

    private void BuildMenu()
    {
        _menu.Dock = DockStyle.Top;
        _menu.RightToLeft = RightToLeft.Yes;
        _menu.Font = new Font("Tahoma", 9.5f);
        _menu.BackColor = ErpTheme.Surface;
        _menu.ForeColor = ErpTheme.Text;

        var home = new ToolStripMenuItem("الرئيسية");
        home.Click += (_, _) => ShowHome();
        _menu.Items.Add(home);

        var refresh = new ToolStripMenuItem("تحديث الصلاحيات");
        refresh.Click += async (_, _) => await LoadSecurityAsync();
        _menu.Items.Add(refresh);

        _menu.Items.Add(new ToolStripSeparator());

        var modules = new ToolStripMenuItem("الوحدات")
        {
            Name = "ModulesMenu"
        };
        _menu.Items.Add(modules);

        var system = new ToolStripMenuItem("النظام");
        system.DropDownItems.Add(
            "اختبار الاتصال",
            null,
            async (_, _) => await CheckConnectionAsync());

        if (_session.GroupId == 1)
        {
            system.DropDownItems.Add(
                "إدارة التراخيص",
                null,
                (_, _) => OpenLicenseManagement());
        }

        system.DropDownItems.Add(new ToolStripSeparator());
        system.DropDownItems.Add("تسجيل الخروج", null, (_, _) => Close());
        _menu.Items.Add(system);
    }

    private void BuildToolbar()
    {
        _toolStrip.Dock = DockStyle.Top;
        _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        _toolStrip.RightToLeft = RightToLeft.Yes;
        _toolStrip.BackColor = ErpTheme.Surface;
        _toolStrip.Padding = new Padding(8, 6, 8, 6);

        var brand = new ToolStripLabel("الصقر للمحاسبة ERP")
        {
            Font = new Font("Tahoma", 11, FontStyle.Bold),
            ForeColor = ErpTheme.Accent,
            Margin = new Padding(4, 2, 12, 2)
        };

        var home = new ToolStripButton("الرئيسية");
        home.Click += (_, _) => ShowHome();

        var refresh = new ToolStripButton("تحديث");
        refresh.Click += async (_, _) => await LoadSecurityAsync();

        var searchLabel = new ToolStripLabel("بحث");
        _search.Width = 300;
        _search.RightToLeft = RightToLeft.Yes;
        _search.Margin = new Padding(8, 1, 8, 1);
        _search.BorderStyle = BorderStyle.FixedSingle;

        var closeCurrent = new ToolStripButton("إغلاق الشاشة");
        closeCurrent.Click += (_, _) =>
        {
            var active = ActiveMdiChild;
            if (active is not null)
                active.Close();
            else
                ShowHome();
        };

        _toolStrip.Items.Add(brand);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(home);
        _toolStrip.Items.Add(refresh);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(searchLabel);
        _toolStrip.Items.Add(new ToolStripControlHost(_search));
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(closeCurrent);
    }

    private void WireEvents()
    {
        Shown += async (_, _) => await LoadSecurityAsync();
        _search.TextChanged += (_, _) =>
        {
            RebuildModuleMenu(_search.Text);
            RebuildNavigation(_search.Text);
        };

        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) =>
            _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
            SetStatusToolTip(groupName);

            RebuildModuleMenu(_search.Text);
            RebuildNavigation(_search.Text);
            ShowHome();
        }
        catch (Exception ex)
        {
            _screens.Clear();
            _status.Text = "تعذر تحميل الصلاحيات";
            _screenCount.Text = "الشاشات: 0";

            MessageBox.Show(
                this,
                "تعذر تحميل الصلاحيات:
" + ex.GetBaseException().Message,
                "الصلاحيات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void SetStatusToolTip(string? groupName)
    {
        _status.ToolTipText =
            $"المستخدم: {_session.UserName}
" +
            $"الفرع: {_session.BranchId?.ToString() ?? "-"}
" +
            $"المجموعة: {groupName ?? _session.GroupId?.ToString() ?? "-"}";
    }

    private void RebuildModuleMenu(string? filter)
    {
        var modules = _menu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(x => x.Name == "ModulesMenu");

        if (modules is null)
            return;

        modules.DropDownItems.Clear();
        var text = filter?.Trim() ?? string.Empty;

        var groups = _screens
            .Where(s => string.IsNullOrWhiteSpace(text) ||
                        s.ScreenName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.ModuleDisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            .GroupBy(s => string.IsNullOrWhiteSpace(s.ModuleDisplayName)
                ? "أخرى"
                : s.ModuleDisplayName)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

        foreach (var group in groups)
        {
            var module = new ToolStripMenuItem(group.Key);

            foreach (var screen in group
                         .OrderBy(s => s.ScreenNum ?? int.MaxValue)
                         .ThenBy(s => s.Id))
            {
                var item = new ToolStripMenuItem(
                    string.IsNullOrWhiteSpace(screen.ScreenName)
                        ? $"شاشة #{screen.Id}"
                        : screen.ScreenName)
                {
                    Tag = screen,
                    Enabled = screen.AllowEnter
                };

                item.Click += ScreenMenu_Click;
                module.DropDownItems.Add(item);
            }

            modules.DropDownItems.Add(module);
        }
    }

    private void RebuildNavigation(string? filter)
    {
        _navigation.SuspendLayout();
        _navigation.Controls.Clear();

        var brand = new Panel
        {
            Width = 228,
            Height = 76,
            BackColor = ErpTheme.Navigation,
            Margin = new Padding(6, 0, 6, 8)
        };

        brand.Controls.Add(new Label
        {
            Text = "الصقر للمحاسبة",
            Dock = DockStyle.Top,
            Height = 32,
            Font = new Font("Tahoma", 14, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleRight
        });

        brand.Controls.Add(new Label
        {
            Text = $"المستخدم: {_session.UserName}",
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = ErpTheme.NavigationMuted,
            TextAlign = ContentAlignment.MiddleRight
        });

        brand.Controls.Add(new Label
        {
            Text = $"الفرع: {_session.BranchId?.ToString() ?? "-"}",
            Dock = DockStyle.Bottom,
            Height = 20,
            ForeColor = ErpTheme.NavigationMuted,
            TextAlign = ContentAlignment.MiddleRight
        });

        _navigation.Controls.Add(brand);

        var homeButton = ErpTheme.CreateNavigationButton("⌂  الرئيسية");
        homeButton.Click += (_, _) => ShowHome();
        _navigation.Controls.Add(homeButton);

        var text = filter?.Trim() ?? string.Empty;

        var groups = _screens
            .Where(s => string.IsNullOrWhiteSpace(text) ||
                        s.ScreenName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.ModuleDisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            .GroupBy(s => string.IsNullOrWhiteSpace(s.ModuleDisplayName)
                ? "أخرى"
                : s.ModuleDisplayName)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

        foreach (var group in groups)
        {
            var moduleLabel = new Label
            {
                Text = group.Key,
                Width = 228,
                Height = 30,
                Margin = new Padding(6, 10, 6, 2),
                ForeColor = ErpTheme.NavigationMuted,
                Font = new Font("Tahoma", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(8, 0, 8, 0)
            };

            _navigation.Controls.Add(moduleLabel);

            foreach (var screen in group
                         .OrderBy(s => s.ScreenNum ?? int.MaxValue)
                         .ThenBy(s => s.Id))
            {
                var name = string.IsNullOrWhiteSpace(screen.ScreenName)
                    ? $"شاشة #{screen.Id}"
                    : screen.ScreenName;

                var button = ErpTheme.CreateNavigationButton("  " + name);
                button.Enabled = screen.AllowEnter;
                button.Tag = screen;
                button.Click += NavigationButton_Click;

                _navigation.Controls.Add(button);
            }
        }

        if (_screens.Count == 0)
        {
            _navigation.Controls.Add(new Label
            {
                Text = "لا توجد شاشات متاحة.",
                Width = 228,
                Height = 40,
                ForeColor = ErpTheme.NavigationMuted,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        _navigation.ResumeLayout();
    }

    private void NavigationButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not ScreenAccess access)
            return;

        OpenAccessScreen(access);
    }

    private void ScreenMenu_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item && item.Tag is ScreenAccess access)
            OpenAccessScreen(access);
    }

    private void OpenAccessScreen(ScreenAccess access)
    {
        if (!access.AllowEnter)
        {
            MessageBox.Show(
                this,
                "لا تملك صلاحية فتح هذه الشاشة.",
                "الصلاحيات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        UseWaitCursor = true;
        try
        {
            _home.Visible = false;

            if (!_router.TryOpen(this, access, out var message) &&
                !string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show(
                    this,
                    message,
                    "فتح الشاشة",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            _home.Visible = true;
            _home.BringToFront();
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void OpenLicenseManagement()
    {
        try
        {
            var service = new LicenseService(
                new DbExecutor(new SqlConnectionFactory(_connectionString)));

            using var form = new LicenseManagementForm(_session, service)
            {
                StartPosition = FormStartPosition.CenterParent
            };

            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.GetBaseException().Message,
                "إدارة التراخيص",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            var tables = await _schema.GetTablesAsync();
            _status.Text =
                $"الاتصال بقاعدة البيانات: سليم — الجداول: {tables.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.GetBaseException().Message,
                "اختبار الاتصال",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ShowHome()
    {
        foreach (var child in MdiChildren)
            child.Close();

        _home.Visible = true;
        _home.BringToFront();
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
            Padding = new Padding(24)
        };

        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "لوحة العمل الرئيسية",
            Dock = DockStyle.Fill,
            Font = new Font("Tahoma", 24f, FontStyle.Bold),
            ForeColor = ErpTheme.Text,
            TextAlign = ContentAlignment.MiddleRight
        };

        var subtitle = new Label
        {
            Text = "إدارة الحسابات والمخزون والمبيعات والمشتريات من مساحة عمل واحدة",
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

        cards.Controls.Add(
            ErpTheme.CreateCard("الشاشات المسموح بها", _screens.Count.ToString("N0"), "حسب User_Screens والصلاحيات"),
            0,
            0);

        cards.Controls.Add(
            ErpTheme.CreateCard("الوحدات", moduleCount.ToString("N0"), "الوحدات المتاحة للمستخدم"),
            1,
            0);

        cards.Controls.Add(
            ErpTheme.CreateCard("الفرع", _session.BranchId?.ToString() ?? "-", "الفرع الحالي"),
            2,
            0);

        cards.Controls.Add(
            ErpTheme.CreateCard("حالة الاتصال", "متصل", "قاعدة GTSdb2026"),
            3,
            0);

        outer.Controls.Add(cards, 0, 2);

        var quickTitle = new Label
        {
            Text = "الوصول السريع",
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
            "الأصناف",
            "العملاء",
            "الموردون",
            "الفواتير",
            "المبيعات",
            "المشتريات",
            "شجرة الحسابات",
            "السندات",
            "الفروع",
            "المخازن",
            "العقود",
            "عروض الأسعار",
            "الجرد"
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
                Width = 176,
                Height = 46,
                Margin = new Padding(6),
                Font = new Font("Tahoma", 9.5f, FontStyle.Bold)
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
            Padding = new Padding(12)
        };

        workspace.Controls.Add(quick);
        outer.Controls.Add(workspace, 0, 4);

        _home.Controls.Add(outer);
    }
}
