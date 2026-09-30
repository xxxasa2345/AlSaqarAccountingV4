using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete schema-driven ERP form used for screens that are not yet backed by
/// a specialized hand-built Form. It renders actual fields from SQL metadata
/// and performs parameterized CRUD when the underlying entity supports it.
/// </summary>
public sealed class DynamicErpScreenForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly DynamicErpScreenService _service;
    private readonly string _screenName;

    private DynamicErpDefinition? _definition;
    private DataTable? _data;
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
        RightToLeft = RightToLeft.Yes,
        BackgroundColor = Color.White,
        RowHeadersVisible = false
    };
    private readonly TextBox _search = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly FlowLayoutPanel _editor = new()
    {
        Dock = DockStyle.Right,
        Width = 420,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(10),
        RightToLeft = RightToLeft.Yes
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(8),
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Dictionary<string, Control> _editors =
        new(StringComparer.OrdinalIgnoreCase);
    private int? _selectedId;

    public DynamicErpScreenForm(
        AppSession session,
        ScreenAccess access,
        DynamicErpScreenService service,
        string screenName)
    {
        _session = session;
        _access = access;
        _service = service;
        _screenName = screenName;

        Text = "الصقر للمحاسبة — " + screenName;
        Width = 1380;
        Height = 820;
        MinimumSize = new Size(1050, 680);
        StartPosition = FormStartPosition.CenterParent;
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
            Height = 108,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        var title = new Label
        {
            Text = _screenName,
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString() ?? "-"}",
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 32, ColumnCount = 2 };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        var refresh = new Button { Text = "تحديث", Dock = DockStyle.Fill, Enabled = _access.AllowEnter };
        refresh.Click += async (_, _) => await LoadAsync();
        searchRow.Controls.Add(_search, 0, 0);
        searchRow.Controls.Add(refresh, 1, 0);
        header.Controls.Add(searchRow);
        header.Controls.Add(info);
        header.Controls.Add(title);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
            WrapContents = false
        };
        AddButton(toolbar, "إضافة", _access.AllowSave, NewRecord);
        AddButton(toolbar, "تعديل", _access.AllowEdit, BeginEdit);
        AddButton(toolbar, "حذف", _access.AllowDelete, DeleteAsync);
        AddButton(toolbar, "تصدير CSV", _access.AllowExport, Export);
        AddButton(toolbar, "طباعة", _access.AllowPrint, Print);

        Controls.Add(_grid);
        Controls.Add(_editor);
        Controls.Add(_status);
        Controls.Add(toolbar);
        Controls.Add(header);
    }

    private void WireEvents()
    {
        _search.TextChanged += (_, _) => ApplySearch();
        _grid.SelectionChanged += (_, _) => BindSelectedRow();
        Shown += async (_, _) => await LoadAsync();
    }

    private static void AddButton(Control parent, string text, bool enabled, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 105,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4)
        };
        button.Click += (_, _) => action();
        ((FlowLayoutPanel)parent).Controls.Add(button);
    }

    private static void AddButton(Control parent, string text, bool enabled, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            Width = 105,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4)
        };
        button.Click += async (_, _) => await action();
        ((FlowLayoutPanel)parent).Controls.Add(button);
    }

    private async Task LoadAsync()
    {
        try
        {
            UseWaitCursor = true;
            _definition ??= await _service.ResolveAsync(_screenName);
            _data = await _service.LoadAsync(_definition, _session.BranchId);
            ScreenToolbox.TranslateCommonColumns(_data);
            _grid.DataSource = _data;
            BuildEditors();
            ApplySearch();
            _status.Text = $"{_screenName} — إجمالي: {_data.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "تعذر تحميل الشاشة:\r\n" + ex.GetBaseException().Message,
                "الشاشة",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _status.Text = ex.GetBaseException().Message;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void BuildEditors()
    {
        if (_definition is null || _editor.Controls.Count > 0) return;

        _editors.Clear();
        foreach (var column in _definition.Columns.Where(c => c.IsWritable))
        {
            var box = new Panel { Width = 380, Height = IsText(column) ? 82 : 62, Margin = new Padding(3) };
            var label = new Label
            {
                Text = Caption(column.Name),
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleRight
            };
            var editor = CreateEditor(column);
            editor.Dock = DockStyle.Fill;
            box.Controls.Add(editor);
            box.Controls.Add(label);
            _editor.Controls.Add(box);
            _editors[column.Name] = editor;
        }

        var save = new Button
        {
            Text = "حفظ السجل",
            Width = 160,
            Height = 36,
            Enabled = _access.AllowSave || _access.AllowEdit,
            Margin = new Padding(4)
        };
        save.Click += async (_, _) => await SaveAsync();
        _editor.Controls.Add(save);

        if (_editor.Controls.Count == 1)
            _editor.Controls.Add(new Label
            {
                Text = "هذه الشاشة مرتبطة بإجراء قراءة فقط؛ عمليات التعديل تعتمد على عقد SQL المتاح.",
                AutoSize = false,
                Width = 380,
                Height = 60,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.DimGray
            });
    }

    private Control CreateEditor(DynamicErpColumn column)
    {
        var type = column.SqlType.ToLowerInvariant();
        if (type is "bit")
            return new CheckBox { Text = Caption(column.Name), RightToLeft = RightToLeft.Yes };

        if (type is "date" or "datetime" or "datetime2" or "smalldatetime")
            return new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };

        if (type is "int" or "bigint" or "smallint" or "tinyint" or
            "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real")
            return new TextBox { RightToLeft = RightToLeft.Yes, TextAlign = HorizontalAlignment.Right };

        var tb = new TextBox { RightToLeft = RightToLeft.Yes };
        if (column.MaxLength <= 0 || column.MaxLength > 500)
            tb.Multiline = true;
        return tb;
    }

    private void BindSelectedRow()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row || _definition is null)
            return;

        _selectedId = null;
        if (!string.IsNullOrWhiteSpace(_definition.KeyColumn) &&
            row.Row.Table.Columns.Contains(_definition.KeyColumn))
        {
            try { _selectedId = Convert.ToInt32(row.Row[_definition.KeyColumn]); }
            catch { }
        }

        foreach (var column in _definition.Columns.Where(c => _editors.ContainsKey(c.Name)))
        {
            var value = row.Row[column.Name];
            var control = _editors[column.Name];

            if (control is CheckBox cb)
                cb.Checked = value != DBNull.Value && Convert.ToBoolean(value);
            else if (control is DateTimePicker dtp)
                dtp.Value = value == DBNull.Value ? DateTime.Now : Convert.ToDateTime(value);
            else
                control.Text = value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty;
        }
    }

    private void NewRecord()
    {
        _selectedId = null;
        foreach (var control in _editors.Values)
        {
            switch (control)
            {
                case CheckBox cb: cb.Checked = false; break;
                case DateTimePicker dtp: dtp.Value = DateTime.Now; break;
                default: control.Text = string.Empty; break;
            }
        }
        _status.Text = "وضع إضافة سجل جديد.";
    }

    private void BeginEdit()
    {
        if (_selectedId.HasValue)
        {
            _status.Text = $"وضع تعديل السجل {_selectedId.Value}.";
            return;
        }
        _status.Text = "حدد سجلاً أولاً.";
    }

    private async Task SaveAsync()
    {
        if (_definition is null) return;

        if (_selectedId.HasValue && !_access.AllowEdit)
        {
            MessageBox.Show(this, "لا تملك صلاحية التعديل.", "الصلاحيات",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_selectedId.HasValue && !_access.AllowSave)
            return;

        try
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in _definition.Columns.Where(c => _editors.ContainsKey(c.Name)))
                values[column.Name] = ReadValue(_editors[column.Name], column);

            UseWaitCursor = true;
            await _service.SaveAsync(_definition, values, _selectedId, _session);
            await LoadAsync();
            _status.Text = _selectedId.HasValue ? "تم تعديل السجل." : "تم حفظ السجل.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "تعذر الحفظ:
" + ex.GetBaseException().Message,
                "حفظ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (!_selectedId.HasValue || _definition is null)
        {
            _status.Text = "حدد سجلاً أولاً.";
            return;
        }
        if (!_access.AllowDelete)
            return;

        if (MessageBox.Show(
                this,
                $"هل تريد حذف السجل {_selectedId.Value}؟",
                "تأكيد الحذف",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _service.DeleteAsync(_definition, _selectedId.Value);
            _selectedId = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر الحذف:
" + ex.GetBaseException().Message,
                "حذف", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ApplySearch()
    {
        if (_data is null) return;
        var value = _search.Text.Trim().Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
        if (string.IsNullOrWhiteSpace(value))
            _data.DefaultView.RowFilter = string.Empty;
        else
        {
            var filters = _data.Columns.Cast<DataColumn>()
                .Where(c => c.DataType == typeof(string))
                .Select(c => $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{value}%'")
                .ToArray();
            _data.DefaultView.RowFilter = filters.Length == 0 ? string.Empty : string.Join(" OR ", filters);
        }
        _status.Text = $"{_screenName} — المعروض: {_data.DefaultView.Count:N0} من {_data.Rows.Count:N0}";
    }

    private void Export()
    {
        if (_data is null) return;
        ScreenToolbox.ExportCsv(this, _data, _screenName);
    }

    private void Print()
    {
        if (_data is null) return;
        ScreenToolbox.ShowPrintPreview(this, _screenName, _data);
    }

    private static object? ReadValue(Control control, DynamicErpColumn column)
    {
        if (control is CheckBox cb)
            return cb.Checked;

        if (control is DateTimePicker dtp)
            return dtp.Value;

        var text = control.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return column.SqlType.ToLowerInvariant() switch
        {
            "int" => int.TryParse(text, out var i) ? i : throw new ArgumentException($"القيمة غير صحيحة: {Caption(column.Name)}"),
            "bigint" => long.TryParse(text, out var l) ? l : throw new ArgumentException($"القيمة غير صحيحة: {Caption(column.Name)}"),
            "smallint" => short.TryParse(text, out var sh) ? sh : throw new ArgumentException($"القيمة غير صحيحة: {Caption(column.Name)}"),
            "tinyint" => byte.TryParse(text, out var by) ? by : throw new ArgumentException($"القيمة غير صحيحة: {Caption(column.Name)}"),
            "decimal" or "numeric" or "money" or "smallmoney" => decimal.TryParse(text, out var d) ? d : throw new ArgumentException($"القيمة غير صحيحة: {Caption(column.Name)}"),
            "float" or "real" => double.TryParse(text, out var f) ? f : throw new ArgumentException($"القيمة غير صحيحة: {Caption(column.Name)}"),
            _ => text
        };
    }

    private static bool IsText(DynamicErpColumn c)
        => c.SqlType is "nvarchar" or "varchar" or "nchar" or "char" || c.MaxLength <= 0 || c.MaxLength > 500;

    private static string Caption(string name)
    {
        return name switch
        {
            "ID" => "المعرف",
            "Name" => "الاسم",
            "BranchID" => "الفرع",
            "Note" => "الملاحظات",
            "NoteNum" => "رقم المستند",
            "Purchases_Date" => "التاريخ",
            "UserID_Add" => "المستخدم",
            _ => name
        };
    }
}
