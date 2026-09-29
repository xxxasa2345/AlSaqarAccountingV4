using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete sales-representatives screen (مندوبو المبيعات). Fully maintained
/// through SalesManService against Account_SalesMan.
/// </summary>
public sealed class SalesMenForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly SalesManService _service;

    private readonly TextBox _name = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _phone = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly NumericUpDown _profit = new()
    {
        Dock = DockStyle.Fill,
        DecimalPlaces = 2,
        Minimum = 0,
        Maximum = 1000000
    };
    private readonly TextBox _search = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
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

    private DataTable? _data;
    private int? _editingSn;

    public SalesMenForm(AppSession session, ScreenAccess access, SalesManService service)
    {
        _session = session;
        _access = access;
        _service = service;

        Text = "الصقر للمحاسبة — مندوبو المبيعات";
        Width = 1100;
        Height = 700;
        MinimumSize = new Size(900, 560);
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
            Height = 122,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        var title = new Label
        {
            Text = "مندوبو المبيعات",
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

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 140,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(8)
        };
        for (var i = 0; i < 3; i++)
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        AddField(editor, "اسم المندوب", _name, 0, 0);
        AddField(editor, "الهاتف", _phone, 1, 0);
        AddField(editor, "نسبة العمولة %", _profit, 2, 0);

        var save = new Button { Text = "حفظ", Dock = DockStyle.Fill, Enabled = _access.AllowSave };
        var cancel = new Button { Text = "إلغاء", Dock = DockStyle.Fill };
        save.Click += async (_, _) => await SaveAsync();
        cancel.Click += (_, _) => ClearEditor();
        editor.Controls.Add(save, 0, 2);
        editor.Controls.Add(cancel, 1, 2);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
            WrapContents = false
        };
        AddButton(toolbar, "تعديل", _access.AllowEdit, BeginEdit);
        AddButton(toolbar, "حذف", _access.AllowDelete, DeleteAsync);
        AddButton(toolbar, "تفاصيل", true, () => ScreenToolbox.ShowRecordDetails(this, "مندوبو المبيعات", _grid));
        AddButton(toolbar, "تصدير CSV", _access.AllowExport, Export);
        AddButton(toolbar, "طباعة", _access.AllowPrint, Print);

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(toolbar);
        Controls.Add(editor);
        Controls.Add(header);
    }

    private static void AddField(TableLayoutPanel editor, string label, Control control, int column, int row)
    {
        var hint = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Height = 24
        };
        editor.Controls.Add(control, column, row);
        editor.Controls.Add(hint, column, row + 1);
    }

    private static void AddButton(FlowLayoutPanel panel, string text, bool enabled, Func<Task> action)
    {
        var button = new Button { Text = text, Width = 100, Height = 30, Enabled = enabled, Margin = new Padding(4) };
        button.Click += async (_, _) => await action();
        panel.Controls.Add(button);
    }

    private static void AddButton(FlowLayoutPanel panel, string text, bool enabled, Action action)
    {
        var button = new Button { Text = text, Width = 105, Height = 30, Enabled = enabled, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
    }

    private void WireEvents()
    {
        _search.TextChanged += (_, _) => ApplySearch();
        _grid.CellDoubleClick += (_, _) => BeginEdit();
        Shown += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            UseWaitCursor = true;
            _data = await _service.ListAsync();
            ScreenToolbox.TranslateCommonColumns(_data);
            _grid.DataSource = _data;
            ApplySearch();
            _status.Text = $"عدد المندوبين: {_data.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ في تحميل المندوبين: " + ex.GetBaseException().Message;
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
        _data.DefaultView.RowFilter = string.IsNullOrWhiteSpace(escaped)
            ? string.Empty
            : $"CONVERT([Name], 'System.String') LIKE '%{escaped}%'";
        _status.Text = $"المعروض: {_data.DefaultView.Count:N0} من {_data.Rows.Count:N0}";
    }

    private async Task SaveAsync()
    {
        if (!_access.AllowSave) return;
        var name = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "اسم المندوب مطلوب.", "تنبيه",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        decimal? profit = _profit.Value > 0 ? _profit.Value : null;

        try
        {
            UseWaitCursor = true;
            if (_editingSn.HasValue)
            {
                if (!_access.AllowEdit) return;
                await _service.UpdateAsync(_editingSn.Value, name, _phone.Text, profit, _session);
                _status.Text = "تم تعديل المندوب بنجاح.";
            }
            else
            {
                var sn = await _service.CreateAsync(name, _phone.Text, profit, _session);
                _status.Text = $"تم حفظ المندوب برقم {sn}.";
            }
            ClearEditor();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "حفظ المندوب",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private Task BeginEdit()
    {
        if (!_access.AllowEdit || _grid.CurrentRow?.DataBoundItem is not DataRowView row)
            return Task.CompletedTask;

        _editingSn = Convert.ToInt32(row.Row["SN"]);
        _name.Text = Convert.ToString(row.Row["Name"]) ?? string.Empty;
        _phone.Text = GetValue(row, "Phone");
        _profit.Value = row.Row.Table.Columns.Contains("profit") &&
                        row.Row["profit"] != DBNull.Value
            ? ClampProfit(Convert.ToDecimal(row.Row["profit"]))
            : 0;
        _name.Focus();
        _name.SelectAll();
        return Task.CompletedTask;
    }

    private static string GetValue(DataRowView row, string column)
        => row.Row.Table.Columns.Contains(column) && row.Row[column] != DBNull.Value
            ? Convert.ToString(row.Row[column]) ?? string.Empty
            : string.Empty;

    private static decimal ClampProfit(decimal value)
    {
        if (value < 0) return 0;
        return value > 1000000 ? 1000000 : value;
    }

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        var sn = Convert.ToInt32(row.Row["SN"]);
        var name = Convert.ToString(row.Row["Name"]) ?? string.Empty;
        if (MessageBox.Show(this, $"هل تريد حذف المندوب؟\r\n{name}", "تأكيد الحذف",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            UseWaitCursor = true;
            await _service.DeleteAsync(sn);
            await LoadAsync();
            _status.Text = "تم حذف المندوب.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "حذف المندوب",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void Export() => ScreenToolbox.ExportCsv(this, _data, "مندوبو المبيعات");

    private void Print() => ScreenToolbox.ShowPrintPreview(this, "مندوبو المبيعات", _data);

    private void ClearEditor()
    {
        _editingSn = null;
        _name.Clear();
        _phone.Clear();
        _profit.Value = 0;
    }
}
