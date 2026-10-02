using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational chart-of-accounts screen (شجرة الحسابات). Loads the original
/// Get_Account_Tree procedure, renders the real account hierarchy in a
/// TreeView, and maintains accounts (add child / rename / delete leaf) through
/// AccountsTreeService with fully parameterized statements.
/// </summary>
public sealed class AccountsTreeForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly AccountsTreeService _service;

    private readonly TreeView _tree = new()
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
        RightToLeft = RightToLeft.Yes
    };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BackgroundColor = Color.White,
        RightToLeft = RightToLeft.Yes,
        RowHeadersVisible = false
    };

    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(8),
        BorderStyle = BorderStyle.FixedSingle
    };

    private readonly TextBox _accountName = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly Label _selectedInfo = new()
    {
        Dock = DockStyle.Fill,
        ForeColor = Color.DimGray,
        TextAlign = ContentAlignment.MiddleRight
    };

    private DataTable? _data;

    public AccountsTreeForm(AppSession session, ScreenAccess access, AccountsTreeService service)
    {
        _session = session;
        _access = access;
        _service = service;

        ErpTheme.ApplyForm(this);
        Text = "الصقر للمحاسبة — شجرة الحسابات";
        Width = 1280;
        Height = 800;
        MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildLayout();
        ErpTheme.ConfigureGrid(_grid);
        WireEvents();
    }

    private void BuildLayout()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 84,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        var title = new Label
        {
            Text = "شجرة الحسابات",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString() ?? "-"} — " +
                   "إضافة حساب فرعي وتعديل الاسم والحذف تعمل الآن مباشرة على قاعدة البيانات.",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(info);
        header.Controls.Add(title);

        var treePanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 420,
            BackColor = Color.FromArgb(246, 248, 251),
            Padding = new Padding(8)
        };
        var treeTitle = new Label
        {
            Text = "الهيكل الشجري",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Tahoma", 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        treePanel.Controls.Add(_tree);
        treePanel.Controls.Add(treeTitle);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
            WrapContents = false
        };
        AddButton(toolbar, "تفاصيل", true, ShowDetails);
        AddButton(toolbar, "تصدير CSV", _access.AllowExport, Export);
        AddButton(toolbar, "طباعة", _access.AllowPrint, Print);
        AddButton(toolbar, "تحديث", _access.AllowEnter, async () => await LoadAsync());

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 84,
            ColumnCount = 5,
            Padding = new Padding(8)
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));

        var nameHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        nameHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        nameHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        nameHost.Controls.Add(new Label
        {
            Text = "اسم الحساب",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Tahoma", 9, FontStyle.Bold),
            AutoSize = true
        }, 0, 0);
        nameHost.Controls.Add(_accountName, 0, 1);

        var add = new Button
        {
            Text = "إضافة حساب فرعي",
            Dock = DockStyle.Fill,
            Enabled = _access.AllowSave,
            FlatStyle = FlatStyle.Flat
        };
        ErpTheme.ConfigureToolbarButton(button);
        add.Click += async (_, _) => await AddAccountAsync();
        var rename = new Button
        {
            Text = "تعديل الاسم",
            Dock = DockStyle.Fill,
            Enabled = _access.AllowEdit,
            FlatStyle = FlatStyle.Flat
        };
        rename.Click += async (_, _) => await RenameAccountAsync();
        var remove = new Button
        {
            Text = "حذف الحساب",
            Dock = DockStyle.Fill,
            Enabled = _access.AllowDelete,
            FlatStyle = FlatStyle.Flat
        };
        remove.Click += async (_, _) => await DeleteAccountAsync();

        editor.Controls.Add(_selectedInfo, 0, 0);
        editor.Controls.Add(nameHost, 1, 0);
        editor.Controls.Add(add, 2, 0);
        editor.Controls.Add(rename, 3, 0);
        editor.Controls.Add(remove, 4, 0);

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(editor);
        Controls.Add(toolbar);
        Controls.Add(treePanel);
        Controls.Add(header);
        Controls.Add(_search);
    }

    private void WireEvents()
    {
        _search.TextChanged += (_, _) => ApplySearch();
        _tree.AfterSelect += (_, _) => UpdateSelectedInfo();
        _tree.NodeMouseDoubleClick += (_, e) => { if (e.Node?.Tag is DataRow row) ShowRowDetails(row); };
        _grid.SelectionChanged += (_, _) => UpdateSelectedInfo();
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.CurrentRow?.DataBoundItem is DataRowView view)
                ShowRowDetails(view.Row);
        };
        Shown += async (_, _) => await LoadAsync();
    }

    private static void AddButton(FlowLayoutPanel toolbar, string text, bool enabled, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 105,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat
        };
        button.Click += (_, _) => action();
        toolbar.Controls.Add(button);
    }

    private async Task LoadAsync()
    {
        try
        {
            UseWaitCursor = true;
            var table = await _service.ListAsync();
            ScreenToolbox.TranslateCommonColumns(table);
            _data = table;
            _grid.DataSource = _data;
            BuildTree(table);
            ApplySearch();
            _status.Text = $"عدد الحسابات: {table.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            _data = null;
            _grid.DataSource = null;
            _tree.Nodes.Clear();
            _status.Text = "تعذر تحميل شجرة الحسابات: " + ex.GetBaseException().Message;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    /// <summary>
    /// Builds the hierarchy from the Account_No / Main_Account_No / Account_Name
    /// columns when the procedure exposes them, otherwise renders a flat list.
    /// </summary>
    private void BuildTree(DataTable table)
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();

            var numberColumn = FindColumn(table, "Account_No");
            var parentColumn = FindColumn(table, "Main_Account_No");
            var nameColumn = FindColumn(table, "Account_Name") ?? FindColumn(table, "Name");
            var fallbackColumn = nameColumn ?? FindColumn(table, "ID");

            if (numberColumn is null || parentColumn is null || nameColumn is null)
            {
                var index = 0;
                foreach (DataRow row in table.Rows)
                {
                    var label = fallbackColumn is not null
                        ? Convert.ToString(row[fallbackColumn]) ?? string.Empty
                        : $"سجل {++index}";
                    _tree.Nodes.Add(new TreeNode(label) { Tag = row });
                }
                return;
            }

            var nodes = new Dictionary<string, TreeNode>();
            var pending = new List<(DataRow Row, string Parent)>();

            foreach (DataRow row in table.Rows)
            {
                var number = Convert.ToString(row[numberColumn]) ?? string.Empty;
                var label = Convert.ToString(row[nameColumn]) ?? string.Empty;
                var node = new TreeNode(string.IsNullOrWhiteSpace(number) ? label : $"{label}  ({number})")
                {
                    Tag = row
                };
                if (!string.IsNullOrWhiteSpace(number))
                    nodes[number] = node;

                var parent = Convert.ToString(row[parentColumn]) ?? string.Empty;
                pending.Add((row, parent));
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (row, parent) in pending)
            {
                var number = Convert.ToString(row[numberColumn]) ?? string.Empty;
                var node = nodes.TryGetValue(number, out var exact) ? exact : null;
                if (node is null) continue;

                var hasParent = !string.IsNullOrWhiteSpace(parent) && parent != "0" && nodes.ContainsKey(parent);
                if (hasParent && !visited.Contains(parent) && parent != number)
                {
                    nodes[parent].Nodes.Add(node);
                    visited.Add(number);
                }
                else
                {
                    _tree.Nodes.Add(node);
                    visited.Add(number);
                }
            }

            _tree.ExpandAll();
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    private static DataColumn? FindColumn(DataTable table, string name)
    {
        foreach (DataColumn column in table.Columns)
            if (string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                return column;
        return null;
    }

    /// <summary>Resolves the account number/name of the current selection
    /// (tree node first, then grid row).</summary>
    private bool TryGetSelectedAccount(out int accountNo, out string accountName)
    {
        accountNo = 0;
        accountName = string.Empty;

        var row = _tree.SelectedNode?.Tag as DataRow
                   ?? (_grid.CurrentRow?.DataBoundItem as DataRowView)?.Row;
        if (row is null)
            return false;

        var numberColumn = FindColumn(row.Table, "Account_No");
        var nameColumn = FindColumn(row.Table, "Account_Name") ?? FindColumn(row.Table, "Name");
        if (numberColumn is null || row[numberColumn] is DBNull)
            return false;

        if (!int.TryParse(Convert.ToString(row[numberColumn]), out accountNo) || accountNo <= 0)
            return false;

        if (nameColumn is not null && row[nameColumn] is not DBNull)
            accountName = Convert.ToString(row[nameColumn]) ?? string.Empty;
        return true;
    }

    private void UpdateSelectedInfo()
    {
        if (TryGetSelectedAccount(out var accountNo, out var accountName))
            _selectedInfo.Text = $"الحساب المحدد: {accountName} ({accountNo})";
        else
            _selectedInfo.Text = "لم يُحدد حساب — اختر حساباً من الشجرة أو الجدول.";
    }

    private async Task AddAccountAsync()
    {
        if (!TryGetSelectedAccount(out var parentNo, out var parentName))
        {
            MessageBox.Show(this, "حدد الحساب الأب أولاً من الشجرة أو الجدول.", "إضافة حساب",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var name = _accountName.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "اكتب اسم الحساب الجديد في حقل الاسم.", "إضافة حساب",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            var accountNo = await _service.CreateAccountAsync(parentNo, name, _session);
            _status.Text = $"تمت إضافة الحساب \"{name}\" برقم {accountNo} تحت \"{parentName}\".";
            _accountName.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر إضافة الحساب:\r\n" + ex.GetBaseException().Message,
                "إضافة حساب", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task RenameAccountAsync()
    {
        if (!TryGetSelectedAccount(out var accountNo, out var currentName))
        {
            MessageBox.Show(this, "حدد الحساب المطلوب تعديله أولاً.", "تعديل الاسم",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var name = _accountName.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "اكتب الاسم الجديد في حقل الاسم.", "تعديل الاسم",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirmation = MessageBox.Show(this,
            $"هل تريد تغيير اسم الحساب {accountNo}؟\r\nمن: {currentName}\r\nإلى: {name}",
            "تأكيد التعديل", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _service.UpdateAccountNameAsync(accountNo, name, _session);
            _status.Text = $"تم تعديل اسم الحساب {accountNo}.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر تعديل الحساب:\r\n" + ex.GetBaseException().Message,
                "تعديل الاسم", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task DeleteAccountAsync()
    {
        if (!TryGetSelectedAccount(out var accountNo, out var accountName))
        {
            MessageBox.Show(this, "حدد الحساب المطلوب حذفه أولاً.", "حذف الحساب",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirmation = MessageBox.Show(this,
            $"هل تريد حذف الحساب \"{accountName}\" ({accountNo})؟\r\n" +
            "يمكن حذف الحسابات الفرعية (بلا أبناء) فقط.",
            "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _service.DeleteAccountAsync(accountNo);
            _status.Text = $"تم حذف الحساب {accountNo}.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف الحساب:\r\n" + ex.GetBaseException().Message,
                "حذف الحساب", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ApplySearch()
    {
        if (_data is null) return;
        var value = _search.Text.Trim().Replace("'", "''");
        var escaped = value.Replace("%", "[%]").Replace("*", "[*]").Replace("[", "[[]");
        if (string.IsNullOrWhiteSpace(escaped))
        {
            _data.DefaultView.RowFilter = string.Empty;
        }
        else
        {
            var filters = _data.Columns
                .Cast<DataColumn>()
                .Where(c => c.DataType == typeof(string))
                .Select(c => $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{escaped}%'")
                .ToArray();
            _data.DefaultView.RowFilter = filters.Length == 0 ? string.Empty : string.Join(" OR ", filters);
        }
        _status.Text = $"المعروض: {_data.DefaultView.Count:N0} من {_data.Rows.Count:N0}";
    }

    private void ShowDetails()
    {
        ScreenToolbox.ShowRecordDetails(this, "شجرة الحسابات", _grid);
    }

    private void ShowRowDetails(DataRow row)
    {
        using var details = new RecordDetailsForm("شجرة الحسابات", row, null);
        details.ShowDialog(this);
    }

    private void Export() => ScreenToolbox.ExportCsv(this, _data, "شجرة الحسابات");

    private void Print() => ScreenToolbox.ShowPrintPreview(this, "شجرة الحسابات", _data);
}
