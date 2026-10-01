using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class ItemUnitForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly ItemUnitService _service;
    private readonly TextBox _name = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _search = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RightToLeft = RightToLeft.Yes };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 32, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
    private DataTable? _data;
    private int? _editingId;

    public ItemUnitForm(AppSession session, ScreenAccess access, ItemUnitService service)
    {
        _session = session;
        _access = access;
        _service = service;
        Text = "الصقر للمحاسبة — الوحدات";
        Width = 1100;
        Height = 700;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        BuildLayout();
        WireEvents();
    }

    private void BuildLayout()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(12), BackColor = Color.FromArgb(245, 247, 250) };
        var title = new Label { Text = "الوحدات", Dock = DockStyle.Top, Height = 38, Font = new Font("Tahoma", 18, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight };
        var info = new Label { Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString() ?? "-"}", Dock = DockStyle.Top, Height = 25, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleRight };
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

        var editor = new TableLayoutPanel { Dock = DockStyle.Top, Height = 76, ColumnCount = 3, Padding = new Padding(8) };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        editor.Controls.Add(_name, 0, 0);
        var save = new Button { Text = "حفظ", Dock = DockStyle.Fill, Enabled = _access.AllowSave || _access.AllowEdit };
        var cancel = new Button { Text = "إلغاء", Dock = DockStyle.Fill };
        save.Click += async (_, _) => await SaveAsync();
        cancel.Click += (_, _) => ClearEditor();
        editor.Controls.Add(save, 1, 0);
        editor.Controls.Add(cancel, 2, 0);
        var hint = new Label { Text = "اسم الوحدة", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Height = 26 };
        editor.Controls.Add(hint, 0, 1);
        editor.SetColumnSpan(hint, 3);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), WrapContents = false };
        AddButton(toolbar, "تعديل", _access.AllowEdit, BeginEditAsync);
        AddButton(toolbar, "حذف", _access.AllowDelete, DeleteAsync);
        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(toolbar);
        Controls.Add(editor);
        Controls.Add(header);
    }

    private void WireEvents()
    {
        _search.TextChanged += (_, _) => ApplySearch();
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && _access.AllowEdit) await BeginEditAsync(); };
        Shown += async (_, _) => await LoadAsync();
    }

    private static void AddButton(FlowLayoutPanel panel, string text, bool enabled, Func<Task> action)
    {
        var button = new Button { Text = text, Width = 100, Height = 30, Enabled = enabled, Margin = new Padding(4) };
        button.Click += async (_, _) => await action();
        panel.Controls.Add(button);
    }

    private async Task LoadAsync()
    {
        try
        {
            UseWaitCursor = true;
            _data = await _service.ListAsync();
            _grid.DataSource = _data;
            ApplySearch();
            _status.Text = $"عدد الوحدات: {_data.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ في تحميل الوحدات: " + ex.GetBaseException().Message;
        }
        finally { UseWaitCursor = false; }
    }

    private void ApplySearch()
    {
        if (_data is null) return;
        var value = _search.Text.Trim().Replace("'", "''");
        var escaped = value.Replace("%", "[%]").Replace("*", "[*]");
        _data.DefaultView.RowFilter = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"CONVERT([Name], 'System.String') LIKE '%{escaped}%'";
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
            MessageBox.Show(this, "اسم الوحدة مطلوب.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            UseWaitCursor = true;
            if (_editingId.HasValue)
            {
                await _service.UpdateAsync(_editingId.Value, name, _session);
                _status.Text = "تم تعديل الوحدة بنجاح.";
            }
            else
            {
                var id = await _service.CreateAsync(name, _session);
                _status.Text = $"تم حفظ الوحدة برقم {id}.";
            }
            ClearEditor();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "حفظ الوحدة", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private Task BeginEditAsync()
    {
        if (!_access.AllowEdit || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return Task.CompletedTask;
        _editingId = Convert.ToInt32(row.Row["ID"]);
        _name.Text = Convert.ToString(row.Row["Name"]) ?? string.Empty;
        _name.Focus();
        _name.SelectAll();
        return Task.CompletedTask;
    }

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        var id = Convert.ToInt32(row.Row["ID"]);
        var name = Convert.ToString(row.Row["Name"]) ?? string.Empty;
        if (MessageBox.Show(this, $"هل تريد حذف الوحدة؟\r\n{name}", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            UseWaitCursor = true;
            await _service.DeleteAsync(id);
            await LoadAsync();
            _status.Text = "تم حذف الوحدة.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "حذف الوحدة", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private void ClearEditor()
    {
        _editingId = null;
        _name.Clear();
    }
}
