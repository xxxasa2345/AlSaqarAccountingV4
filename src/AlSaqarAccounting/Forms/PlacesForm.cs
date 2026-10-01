using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete operational form for the original FrmPlace screen.
/// </summary>
public sealed class PlacesForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly PlacesService _service;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = true,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RightToLeft = RightToLeft.Yes,
        RowHeadersVisible = false
    };
    private readonly TextBox _name = new()
    {
        Dock = DockStyle.Fill,
        RightToLeft = RightToLeft.Yes
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(8)
    };
    private int? _editingId;

    public PlacesForm(AppSession session, ScreenAccess access, PlacesService service)
    {
        _session = session;
        _access = access;
        _service = service;

        Text = "مناطق المناديب — قاعدة البيانات الأصلية";
        Width = 900;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildUi();
        Shown += async (_, _) => await ReloadAsync();
    }

    private void BuildUi()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 112,
            Padding = new Padding(12)
        };

        var title = new Label
        {
            Text = "مناطق المناديب",
            Dock = DockStyle.Top,
            Height = 35,
            Font = new Font("Tahoma", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };

        var input = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            ColumnCount = 3
        };
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        var save = new Button
        {
            Text = "حفظ",
            Dock = DockStyle.Fill,
            Enabled = _access.AllowSave || _access.AllowEdit
        };
        save.Click += async (_, _) => await SaveAsync();

        var add = new Button
        {
            Text = "منطقة جديدة",
            Dock = DockStyle.Fill,
            Enabled = _access.AllowSave
        };
        add.Click += (_, _) => ClearEditor();

        input.Controls.Add(_name, 0, 0);
        input.Controls.Add(save, 1, 0);
        input.Controls.Add(add, 2, 0);

        header.Controls.Add(input);
        header.Controls.Add(title);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };

        var delete = new Button
        {
            Text = "حذف",
            Width = 100,
            Enabled = _access.AllowDelete
        };
        delete.Click += async (_, _) => await DeleteAsync();

        var refresh = new Button
        {
            Text = "تحديث",
            Width = 100,
            Enabled = _access.AllowEnter
        };
        refresh.Click += async (_, _) => await ReloadAsync();

        toolbar.Controls.Add(delete);
        toolbar.Controls.Add(refresh);

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(toolbar);
        Controls.Add(header);

        _grid.SelectionChanged += (_, _) => LoadSelected();
        _grid.CellDoubleClick += async (_, _) =>
        {
            if (_access.AllowEdit)
                await Task.CompletedTask;
        };
    }

    private async Task ReloadAsync()
    {
        try
        {
            UseWaitCursor = true;
            _grid.DataSource = await _service.GetPlacesAsync();
            ScreenToolbox.TranslateCommonColumns((_grid.DataSource as DataTable)!);
            _status.Text = $"مناطق المناديب — الإجمالي: {_grid.Rows.Count:N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "تعذر تحميل مناطق المناديب:\r\n" + ex.GetBaseException().Message,
                "مناطق المناديب",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void LoadSelected()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView row)
            return;

        _editingId = Convert.ToInt32(row["ID"]);
        _name.Text = Convert.ToString(row["Name"]) ?? string.Empty;
    }

    private async Task SaveAsync()
    {
        if (_editingId.HasValue && !_access.AllowEdit)
            return;
        if (!_editingId.HasValue && !_access.AllowSave)
            return;

        try
        {
            UseWaitCursor = true;
            await _service.SaveAsync(_editingId, _name.Text, _session);
            ClearEditor();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "تعذر حفظ المنطقة:\r\n" + ex.GetBaseException().Message,
                "مناطق المناديب",
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
        if (!_editingId.HasValue || !_access.AllowDelete)
            return;

        if (MessageBox.Show(this,
            "هل تريد حذف المنطقة المحددة؟",
            "تأكيد الحذف",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _service.DeleteAsync(_editingId.Value);
            ClearEditor();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "تعذر حذف المنطقة:\r\n" + ex.GetBaseException().Message,
                "مناطق المناديب",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ClearEditor()
    {
        _editingId = null;
        _name.Clear();
        _grid.ClearSelection();
        _name.Focus();
    }
}
