using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace AlSaqarAccounting.UI;

/// <summary>
/// Operational first-wave screen for master/transaction lists.
/// It reads through the verified original SELECT procedures when available,
/// otherwise through a whitelisted source table. No database writes are performed.
/// </summary>
public sealed class OperationalDataScreen : Form
{
    private readonly string _connectionString;
    private readonly OperationalScreenDefinition _definition;
    private readonly ScreenAccess _access;
    private readonly int? _branchId;

    private readonly TextBox _search = new()
    {
        Dock = DockStyle.Fill,
        RightToLeft = RightToLeft.Yes,
        PlaceholderText = "بحث داخل البيانات..."
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
        Padding = new Padding(8),
        TextAlign = ContentAlignment.MiddleRight,
        BorderStyle = BorderStyle.FixedSingle
    };

    private DataTable? _data;

    public OperationalDataScreen(
        string connectionString,
        OperationalScreenDefinition definition,
        ScreenAccess access,
        int? branchId)
    {
        _connectionString = connectionString;
        _definition = definition;
        _access = access;
        _branchId = branchId;

        Text = "الصقر للمحاسبة — " + definition.DisplayName;
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 650);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildLayout();
        WireEvents();
    }

    private void BuildLayout()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 104,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(12)
        };

        var title = new Label
        {
            Text = _definition.DisplayName,
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };

        var permissions = new Label
        {
            Text = BuildPermissionText(),
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };

        var searchPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            ColumnCount = 2,
            RowCount = 1
        };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        var refresh = new Button
        {
            Text = "تحديث",
            Dock = DockStyle.Fill,
            Enabled = _access.AllowEnter
        };
        refresh.Click += (_, _) => LoadData();

        searchPanel.Controls.Add(_search, 0, 0);
        searchPanel.Controls.Add(refresh, 1, 0);

        header.Controls.Add(searchPanel);
        header.Controls.Add(permissions);
        header.Controls.Add(title);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
            WrapContents = false
        };

        var details = MakeButton("تفاصيل", true);
        details.Click += (_, _) => ShowSelectedRecord();

        var export = MakeButton("تصدير CSV", _access.AllowExport);
        export.Click += (_, _) => ExportCsv();

        var print = MakeButton("طباعة", _access.AllowPrint);
        print.Click += (_, _) => PrintGrid();

        var save = MakeButton("حفظ", false);
        var edit = MakeButton("تعديل", false);
        var delete = MakeButton("حذف", false);

        ConfigureFutureAction(save, "الحفظ سيُربط بالـStored Procedure الأصلي بعد اكتمال عقد الشاشة.");
        ConfigureFutureAction(edit, "التعديل سيُربط بالـStored Procedure الأصلي بعد اكتمال عقد الشاشة.");
        ConfigureFutureAction(delete, "الحذف سيُربط بالـStored Procedure الأصلي بعد اكتمال عقد الشاشة.");

        toolbar.Controls.Add(details);
        toolbar.Controls.Add(export);
        toolbar.Controls.Add(print);
        toolbar.Controls.Add(save);
        toolbar.Controls.Add(edit);
        toolbar.Controls.Add(delete);

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(toolbar);
        Controls.Add(header);

        Shown += (_, _) => LoadData();
    }

    private Button MakeButton(string text, bool enabled)
    {
        return new Button
        {
            Text = text,
            Width = 105,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat
        };
    }

    private static void ConfigureFutureAction(Button button, string message)
    {
        button.Tag = message;
        button.Click += (_, _) =>
        {
            MessageBox.Show(
                message,
                "ربط منطق الأعمال",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
    }

    private string BuildPermissionText()
    {
        var parts = new List<string> { "فتح" };
        if (_access.AllowSave) parts.Add("حفظ");
        if (_access.AllowEdit) parts.Add("تعديل");
        if (_access.AllowDelete) parts.Add("حذف");
        if (_access.AllowPrint) parts.Add("طباعة");
        if (_access.AllowExport) parts.Add("تصدير");
        if (_access.AllowBranch) parts.Add("تبديل الفرع");
        return "الصلاحيات: " + string.Join("  |  ", parts);
    }

    private void WireEvents()
    {
        _search.TextChanged += (_, _) => ApplySearch(_search.Text);
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                ShowSelectedRecord();
        };
    }

    private void LoadData()
    {
        try
        {
            UseWaitCursor = true;
            _data = _definition.StoredProcedure is not null
                ? LoadStoredProcedure(_definition.StoredProcedure, _definition.PassBranchId)
                : LoadTable(_definition.TableName!);

            TranslateColumns(_data);
            _grid.DataSource = _data;
            _status.Text = BuildStatus();
            ApplySearch(_search.Text);
        }
        catch (Exception ex)
        {
            _data = null;
            _grid.DataSource = null;
            _status.Text = "تعذر تحميل البيانات: " + ex.GetBaseException().Message;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private DataTable LoadStoredProcedure(string procedureName, bool passBranch)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 90
        };

        if (passBranch)
        {
            if (!_branchId.HasValue)
                throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");

            command.Parameters.Add("@BranchID", SqlDbType.Int).Value = _branchId.Value;
        }

        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }

    private DataTable LoadTable(string tableName)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Item_Company",
            "Item_Unit",
            "Item_Class",
            "Item_Groups",
            "Account_CustSup",
            "Account_Branch"
        };

        if (!allowed.Contains(tableName))
            throw new InvalidOperationException("مصدر الشاشة غير موجود في قائمة المصادر المسموح بها.");

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        var safe = tableName.Replace("]", "]]", StringComparison.Ordinal);
        using var command = new SqlCommand(
            $"SELECT TOP (2000) * FROM dbo.[{safe}]",
            connection)
        {
            CommandTimeout = 90
        };

        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }

    private void ApplySearch(string? text)
    {
        if (_data is null)
            return;

        var value = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            _data.DefaultView.RowFilter = string.Empty;
            _status.Text = BuildStatus();
            return;
        }

        var escaped = value
            .Replace("'", "''", StringComparison.Ordinal)
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("*", "[*]", StringComparison.Ordinal);

        var filters = _data.Columns
            .Cast<DataColumn>()
            .Where(c => c.DataType == typeof(string))
            .Select(c => $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{escaped}%'")
            .ToArray();

        _data.DefaultView.RowFilter = filters.Length == 0
            ? string.Empty
            : string.Join(" OR ", filters);

        _status.Text = BuildStatus();
    }

    private string BuildStatus()
    {
        var shown = _data?.DefaultView.Count ?? 0;
        var total = _data?.Rows.Count ?? 0;
        var source = _definition.StoredProcedure is not null
            ? "الإجراء: " + _definition.StoredProcedure
            : "الجدول: " + _definition.TableName;
        return $"{source} — إجمالي: {total:N0} — المعروض: {shown:N0}";
    }

    private void ShowSelectedRecord()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row)
        {
            MessageBox.Show("حدد سجلًا أولاً.", "تفاصيل", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var details = new RecordDetailsForm(
            _definition.DisplayName,
            row.Row,
            _definition.KeyColumn is not null && _data?.Columns.Contains(_definition.KeyColumn) == true
                ? row.Row[_definition.KeyColumn]
                : null);
        details.ShowDialog(this);
    }

    private void ExportCsv()
    {
        if (!_access.AllowExport || _data is null)
            return;

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV UTF-8 (*.csv)|*.csv",
            FileName = SanitizeFileName(_definition.DisplayName) + ".csv",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var sb = new StringBuilder();

        for (int i = 0; i < _data.Columns.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeCsv(_data.Columns[i].ColumnName));
        }
        sb.AppendLine();

        foreach (DataRowView view in _data.DefaultView)
        {
            for (int i = 0; i < _data.Columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(view.Row[i] == DBNull.Value ? string.Empty : Convert.ToString(view.Row[i]) ?? string.Empty));
            }
            sb.AppendLine();
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _status.Text = "تم التصدير: " + dialog.FileName;
    }

    private void PrintGrid()
    {
        if (!_access.AllowPrint)
            return;

        using var preview = new Form
        {
            Text = "معاينة الطباعة — " + _definition.DisplayName,
            Width = 1100,
            Height = 750,
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };

        var copy = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            RightToLeft = RightToLeft.Yes,
            DataSource = _data?.DefaultView.ToTable()
        };

        preview.Controls.Add(copy);
        preview.ShowDialog(this);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return value;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "export" : value;
    }

    private static void TranslateColumns(DataTable table)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ID"] = "المعرف",
            ["ItemId"] = "رقم الصنف",
            ["Item_code"] = "كود الصنف",
            ["item_Name"] = "اسم الصنف",
            ["item_Name_English"] = "الاسم الإنجليزي",
            ["Account_No"] = "رقم الحساب",
            ["Account_Name"] = "اسم الحساب",
            ["BranchName"] = "اسم الفرع",
            ["SupplierName"] = "اسم العميل/المورد",
            ["SupplierPhone"] = "الهاتف",
            ["SupplierVatNum"] = "الرقم الضريبي",
            ["Purchases_Date"] = "التاريخ",
            ["Net"] = "الصافي",
            ["Net1"] = "الصافي",
            ["Tax"] = "الضريبة",
            ["TotalPrices"] = "الإجمالي",
            ["Cash"] = "النقدي",
            ["Bank"] = "البنك",
            ["PurchasesType"] = "نوع العملية",
            ["Note"] = "الملاحظات",
            ["Name"] = "الاسم"
        };

        foreach (DataColumn column in table.Columns)
        {
            if (map.TryGetValue(column.ColumnName, out var arabic))
                column.Caption = arabic;
        }
    }
}

internal sealed class RecordDetailsForm : Form
{
    public RecordDetailsForm(string title, DataRow row, object? key)
    {
        Text = "تفاصيل — " + title;
        Width = 720;
        Height = 680;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            RightToLeft = RightToLeft.Yes
        };

        grid.Columns.Add("Field", "الحقل");
        grid.Columns.Add("Value", "القيمة");

        foreach (DataColumn c in row.Table.Columns)
        {
            var value = row[c] == DBNull.Value ? string.Empty : Convert.ToString(row[c]) ?? string.Empty;
            grid.Rows.Add(c.Caption, value);
        }

        if (key is not null && key != DBNull.Value)
            Text += $" — {key}";

        Controls.Add(grid);
    }
}
