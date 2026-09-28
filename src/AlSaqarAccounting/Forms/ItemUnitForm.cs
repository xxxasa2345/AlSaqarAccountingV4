using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;
using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.Forms;

public sealed class ItemUnitForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly ItemUnitService _service;
    private readonly TextBox _search = new();
    private readonly TextBox _name = new();
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();
    private DataTable? _data;
    private int? _editingId;

    public ItemUnitForm(AppSession session, ScreenAccess access, ItemUnitService service)
    {
        _session = session;
        _access = access;
        _service = service;
        Text = "وحدات الأصناف";
        Width = 900;
        Height = 600;
        StartPosition = FormStartPosition.CenterParent;
        BuildUi();
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(8) };
        _search.Width = 280;
        _search.PlaceholderText = "بحث...";
        _search.TextChanged += (_, _) => ApplySearch();
        _name.Width = 220;

        var save = new Button { Text = "حفظ", Width = 90 };
        save.Click += async (_, _) => await SaveAsync();
        var refresh = new Button { Text = "تحديث", Width = 90 };
        refresh.Click += async (_, _) => await LoadAsync();
        var clear = new Button { Text = "جديد", Width = 90 };
        clear.Click += (_, _) => ClearEditor();

        top.Controls.Add(new Label { Text = "اسم الوحدة", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        top.Controls.Add(_name);
        top.Controls.Add(save);
        top.Controls.Add(clear);
        top.Controls.Add(refresh);
        top.Controls.Add(new Label { Text = "بحث", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        top.Controls.Add(_search);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AutoGenerateColumns = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.CellDoubleClick += (_, _) => LoadSelected();

        _status.Dock = DockStyle.Bottom;
        _status.Height = 28;
        _status.TextAlign = ContentAlignment.MiddleRight;

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(top);
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

    private void LoadSelected()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        if (!row.Row.Table.Columns.Contains("ID") || !row.Row.Table.Columns.Contains("Name")) return;
        _editingId = row.Row.Field<int?>("ID");
        _name.Text = row.Row.Field<string>("Name") ?? string.Empty;
    }

    private async Task SaveAsync()
    {
        if (!_access.AllowSave) return;
        var name = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("اسم الوحدة مطلوب.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            UseWaitCursor = true;
            if (_editingId.HasValue)
            {
                if (!_access.AllowEdit) return;
                await _service.UpdateAsync(_editingId.Value, name, _session);
            }
            else
            {
                await _service.CreateAsync(name, _session);
            }

            ClearEditor();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private void ClearEditor()
    {
        _editingId = null;
        _name.Clear();
        _name.Focus();
    }
}
