using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete stores screen (المخازن). Fully maintained through StoresService
/// against Account_Stores.
/// </summary>
public sealed class StoresForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly StoresService _service;

    private readonly TextBox _name = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _phone = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _address = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
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
    private int? _editingId;

    public StoresForm(AppSession session, ScreenAccess access, StoresService service)
    {
        _session = session;
        _access = access;
        _service = service;

        Text = "الصقر للمحاسبة — المخازن";
        Width = 1180;
        Height = 720;
        MinimumSize = new Size(960, 580);
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
            Text = "المخازن",
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
        AddField(editor, "اسم المخزن", _name, 0, 0);
        AddField(editor, "الهاتف", _phone, 1, 0);
        AddField(editor, "العنوان", _address, 2, 0);

        var save = new Button { Text = "حفظ", Dock = DockStyle.Fill, Enabled = _access.AllowSave || _access.AllowEdit };
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
        AddButton(toolbar, "تفاصيل", true, () => ScreenToolbox.ShowRecordDetails(this, "المخازن", _grid));
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
            _status.Text = $"عدد المخازن: {_data.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ في تحميل المخازن: " + ex.GetBaseException().Message;
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
            : $"CONVERT([Store_Name], 'System.String') LIKE '%{escaped}%'";
        _status.Text = $"المعروض: {_data.DefaultView.Count:N0} من {_data.Rows.Count:N0}";
    }

    private async Task SaveAsync()
    {
        if (_editingId.HasValue)
        {
            if (!_access.AllowEdit) return;
        }
        else if (!_access.AllowSave)
        {
            return;
        }
        var name = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "اسم المخزن مطلوب.", "تنبيه",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            UseWaitCursor = true;
            if (_editingId.HasValue)
            {
                await _service.UpdateAsync(_editingId.Value, name, _phone.Text, _address.Text, _session);
                _status.Text = "تم تعديل المخزن بنجاح.";
            }
            else
            {
                var id = await _service.CreateAsync(name, _phone.Text, _address.Text, _session);
                _status.Text = $"تم حفظ المخزن برقم {id}.";
            }
            ClearEditor();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "حفظ المخزن",
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

        _editingId = Convert.ToInt32(row.Row["ID"]);
        _name.Text = Convert.ToString(row.Row["Store_Name"]) ?? string.Empty;
        _phone.Text = GetValue(row, "Phone");
        _address.Text = GetValue(row, "Address");
        _name.Focus();
        _name.SelectAll();
        return Task.CompletedTask;
    }

    private static string GetValue(DataRowView row, string column)
        => row.Row.Table.Columns.Contains(column) && row.Row[column] != DBNull.Value
            ? Convert.ToString(row.Row[column]) ?? string.Empty
            : string.Empty;

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        var id = Convert.ToInt32(row.Row["ID"]);
        var name = Convert.ToString(row.Row["Store_Name"]) ?? string.Empty;
        if (MessageBox.Show(this, $"هل تريد حذف المخزن؟\r\n{name}", "تأكيد الحذف",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            UseWaitCursor = true;
            await _service.DeleteAsync(id);
            await LoadAsync();
            _status.Text = "تم حذف المخزن.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "حذف المخزن",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void Export() => ScreenToolbox.ExportCsv(this, _data, "المخازن");

    private void Print() => ScreenToolbox.ShowPrintPreview(this, "المخازن", _data);

    private void ClearEditor()
    {
        _editingId = null;
        _name.Clear();
        _phone.Clear();
        _address.Clear();
    }
}
