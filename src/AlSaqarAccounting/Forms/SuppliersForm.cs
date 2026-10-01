using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete suppliers screen (الموردون). Fully maintained through
/// CustSupService against Account_CustSup where IsSuppliers = 1.
/// </summary>
public sealed class SuppliersForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly CustSupService _service;

    private readonly TextBox _name = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _vat = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
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

    public SuppliersForm(AppSession session, ScreenAccess access, CustSupService service)
    {
        _session = session;
        _access = access;
        _service = service;

        Text = "الصقر للمحاسبة — الموردون";
        Width = 1180;
        Height = 740;
        MinimumSize = new Size(960, 600);
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
            Text = "الموردون",
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
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(8)
        };
        for (var i = 0; i < 4; i++)
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddField(editor, "اسم المورد", _name, 0, 0);
        AddField(editor, "الرقم الضريبي", _vat, 1, 0);
        AddField(editor, "الهاتف", _phone, 2, 0);
        AddField(editor, "العنوان", _address, 3, 0);

        var save = new Button { Text = "حفظ", Dock = DockStyle.Fill, Enabled = _access.AllowSave || _access.AllowEdit };
        var cancel = new Button { Text = "إلغاء", Dock = DockStyle.Fill };
        save.Click += async (_, _) => await SaveAsync();
        cancel.Click += (_, _) => ClearEditor();
        editor.Controls.Add(save, 0, 2);
        editor.Controls.Add(cancel, 2, 2);
        editor.SetColumnSpan(save, 2);
        editor.SetColumnSpan(cancel, 2);

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
        AddButton(toolbar, "تفاصيل", true, () => ScreenToolbox.ShowRecordDetails(this, "الموردون", _grid));
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
            _data = await _service.ListSuppliersAsync();
            ScreenToolbox.TranslateCommonColumns(_data);
            _grid.DataSource = _data;
            ApplySearch();
            _status.Text = $"عدد الموردين: {_data.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            _status.Text = "خطأ في تحميل الموردين: " + ex.GetBaseException().Message;
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
            : $"CONVERT([CustSuppName], 'System.String') LIKE '%{escaped}%'";
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
            MessageBox.Show(this, "اسم المورد مطلوب.", "تنبيه",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            UseWaitCursor = true;
            if (_editingId.HasValue)
            {
                await _service.UpdateSupplierAsync(
                    _editingId.Value, name, _vat.Text, _phone.Text, _address.Text, _session);
                _status.Text = "تم تعديل المورد بنجاح.";
            }
            else
            {
                var id = await _service.CreateSupplierAsync(
                    name, _vat.Text, _phone.Text, _address.Text, _session);
                _status.Text = $"تم حفظ المورد برقم {id}.";
            }
            ClearEditor();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "حفظ المورد",
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
        _name.Text = Convert.ToString(row.Row["CustSuppName"]) ?? string.Empty;
        _vat.Text = row.Row.Table.Columns.Contains("VatNum") && row.Row["VatNum"] != DBNull.Value
            ? Convert.ToString(row.Row["VatNum"]) ?? string.Empty
            : string.Empty;
        _phone.Text = row.Row.Table.Columns.Contains("Phone") && row.Row["Phone"] != DBNull.Value
            ? Convert.ToString(row.Row["Phone"]) ?? string.Empty
            : string.Empty;
        _address.Text = row.Row.Table.Columns.Contains("Address") && row.Row["Address"] != DBNull.Value
            ? Convert.ToString(row.Row["Address"]) ?? string.Empty
            : string.Empty;
        _name.Focus();
        _name.SelectAll();
        return Task.CompletedTask;
    }

    private async Task DeleteAsync()
    {
        if (!_access.AllowDelete || _grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        var id = Convert.ToInt32(row.Row["ID"]);
        var name = Convert.ToString(row.Row["CustSuppName"]) ?? string.Empty;
        if (MessageBox.Show(this, $"هل تريد حذف المورد؟\r\n{name}", "تأكيد الحذف",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            UseWaitCursor = true;
            await _service.DeleteSupplierAsync(id);
            await LoadAsync();
            _status.Text = "تم حذف المورد.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "حذف المورد",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void Export() => ScreenToolbox.ExportCsv(this, _data, "الموردون");

    private void Print() => ScreenToolbox.ShowPrintPreview(this, "الموردون", _data);

    private void ClearEditor()
    {
        _editingId = null;
        _name.Clear();
        _vat.Clear();
        _phone.Clear();
        _address.Clear();
    }
}
