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

    private readonly TreeView _navigation = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        HideSelection = false,
        RightToLeft = RightToLeft.Yes,
        Font = new Font("Tahoma", 10)
    };

    private readonly TextBox _search = new()
    {
        Dock = DockStyle.Top,
        Height = 34,
        PlaceholderText = "بحث في الشاشات...",
        RightToLeft = RightToLeft.Yes
    };

    private readonly Label _title = new()
    {
        Dock = DockStyle.Top,
        Height = 56,
        TextAlign = ContentAlignment.MiddleRight,
        Font = new Font("Tahoma", 19, FontStyle.Bold),
        ForeColor = Color.FromArgb(35, 48, 68)
    };

    private readonly Label _subtitle = new()
    {
        Dock = DockStyle.Top,
        Height = 30,
        TextAlign = ContentAlignment.MiddleRight,
        ForeColor = Color.DimGray
    };

    private readonly Panel _content = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(24),
        BackColor = Color.White
    };

    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 30,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(8),
        BorderStyle = BorderStyle.FixedSingle
    };

    private List<ScreenAccess> _screens = new();

    public MainForm(
        AppSession session,
        SchemaService schema,
        StoredProcedureExecutor sp,
        SecurityService security)
    {
        _session = session;
        _schema = schema;
        _sp = sp;
        _security = security;
        _router = new ScreenRouter(LoadConnectionString(), session);

        Text = $"الصقر للمحاسبة — {session.UserName}";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildLayout();
        WireEvents();
        ShowHome();
    }

    private void BuildLayout()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = Color.FromArgb(28, 54, 86)
        };

        var brand = new Label
        {
            Text = "الصقر للمحاسبة",
            Dock = DockStyle.Right,
            Width = 360,
            ForeColor = Color.White,
            Font = new Font("Tahoma", 21, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var userInfo = new Label
        {
            Text = $"المستخدم: {_session.UserName}    |    الفرع: {_session.BranchId?.ToString() ?? "-"}    |    المجموعة: {_session.GroupId?.ToString() ?? "-"}",
            Dock = DockStyle.Fill,
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Tahoma", 10),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0)
        };

        header.Controls.Add(userInfo);
        header.Controls.Add(brand);

        var navPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 350,
            BackColor = Color.FromArgb(246, 248, 251),
            Padding = new Padding(10)
        };

        var navTitle = new Label
        {
            Text = "القائمة الرئيسية",
            Dock = DockStyle.Top,
            Height = 38,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Tahoma", 13, FontStyle.Bold)
        };

        var refresh = new Button
        {
            Text = "تحديث الصلاحيات",
            Dock = DockStyle.Bottom,
            Height = 36,
            FlatStyle = FlatStyle.Flat
        };
        refresh.Click += async (_, _) => await LoadSecurityAsync();

        navPanel.Controls.Add(_navigation);
        navPanel.Controls.Add(refresh);
        navPanel.Controls.Add(_search);
        navPanel.Controls.Add(navTitle);

        Controls.Add(_content);
        Controls.Add(_status);
        Controls.Add(navPanel);
        Controls.Add(header);
        Controls.Add(_subtitle);
        Controls.Add(_title);
    }

    private void WireEvents()
    {
        _navigation.NodeMouseDoubleClick += Navigation_NodeMouseDoubleClick;
        _search.TextChanged += (_, _) => RebuildNavigation(_search.Text);
        Shown += async (_, _) => await LoadSecurityAsync();
    }

    private async Task LoadSecurityAsync()
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            _screens = (await _security.GetAccessibleScreensAsync(_session)).ToList();
            var groupName = await _security.GetGroupNameAsync(_session);

            _status.Text = $"عدد الشاشات المسموح بها: {_screens.Count:N0} | المجموعة: {groupName ?? "-"}";
            _subtitle.Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString() ?? "-"} | المجموعة: {groupName ?? _session.GroupId?.ToString() ?? "-"}";
            RebuildNavigation(_search.Text);
        }
        catch (Exception ex)
        {
            _screens.Clear();
            _status.Text = "تعذر تحميل الصلاحيات: " + ex.GetBaseException().Message;
            MessageBox.Show(
                ex.GetBaseException().Message,
                "الصلاحيات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RebuildNavigation(string? filter)
    {
        _navigation.BeginUpdate();
        try
        {
            _navigation.Nodes.Clear();

            var text = filter?.Trim() ?? string.Empty;
            var groups = _screens
                .Where(s => string.IsNullOrWhiteSpace(text) ||
                            s.ScreenName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                            s.ModuleDisplayName.Contains(text, StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => s.ModuleDisplayName)
                .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

            foreach (var group in groups)
            {
                var moduleNode = new TreeNode($"{group.Key}  ({group.Count():N0})")
                {
                    ForeColor = Color.FromArgb(28, 54, 86)
                };

                foreach (var screen in group.OrderBy(s => s.ScreenNum ?? int.MaxValue).ThenBy(s => s.Id))
                {
                    var node = new TreeNode(
                        string.IsNullOrWhiteSpace(screen.ScreenName)
                            ? $"شاشة #{screen.Id}"
                            : screen.ScreenName)
                    {
                        Tag = screen
                    };
                    moduleNode.Nodes.Add(node);
                }

                _navigation.Nodes.Add(moduleNode);
                moduleNode.Expand();
            }
        }
        finally
        {
            _navigation.EndUpdate();
        }
    }

    private void Navigation_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node.Tag is not ScreenAccess screen)
            return;

        if (!screen.AllowEnter)
        {
            MessageBox.Show("لا تملك صلاحية فتح هذه الشاشة.", "الصلاحيات",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            if (!_router.TryOpen(this, screen, out var message) && !string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message, "فتح الشاشة", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ShowHome()
    {
        _title.Text = "الرئيسية";
        _content.Controls.Clear();

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(6)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(MakeInfoCard("المستخدم", _session.UserName), 0, 0);
        panel.Controls.Add(MakeInfoCard("الفرع", _session.BranchId?.ToString() ?? "-"), 1, 0);
        panel.Controls.Add(MakeInfoCard("المجموعة", _session.GroupId?.ToString() ?? "-"), 0, 1);
        panel.Controls.Add(MakeInfoCard("الشاشات المسموح بها", "يتم تحميلها من User_Permission"), 1, 1);

        var note = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Tahoma", 14, FontStyle.Bold),
            ForeColor = Color.DimGray,
            Text = "اختر الوحدة ثم افتح الشاشة بنقرة مزدوجة.\r\nالصلاحيات تُقرأ مباشرة من User_Groups / User_Permission / User_Screens."
        };
        panel.Controls.Add(note, 0, 2);
        panel.SetColumnSpan(note, 2);

        _content.Controls.Add(panel);
    }

    private static Panel MakeInfoCard(string title, string value)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 250, 253)
        };

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"{title}\r\n{value}",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Tahoma", 12, FontStyle.Bold)
        };

        card.Controls.Add(label);
        return card;
    }

    private string LoadConnectionString()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("لم يتم العثور على appsettings.json", path);

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("GtsDb2026")
            .GetString()
            ?? throw new InvalidOperationException("ConnectionStrings:GtsDb2026 غير موجود.");
    }
}
