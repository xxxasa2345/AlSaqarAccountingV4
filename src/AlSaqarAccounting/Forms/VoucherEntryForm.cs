using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational voucher entry screen (سند قبض / سند صرف). Builds a balanced
/// Voucher and saves it through VouchersService → the original
/// dbo.Insert_Tran_Tran (with @Transn OUTPUT) + dbo.INSERT_Tran_TranDetails
/// procedures. No direct SQL in the form.
/// </summary>
public sealed class VoucherEntryForm : Form
{
    private readonly AppSession _session;
    private readonly ScreenAccess _access;
    private readonly VouchersService _vouchers;

    private readonly ComboBox _typeCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _accountCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, RightToLeft = RightToLeft.Yes };
    private readonly DateTimePicker _date = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
    private readonly TextBox _docCode = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _note = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _lineDescription = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
    private readonly NumericUpDown _debit = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2 };
    private readonly NumericUpDown _credit = new() { Dock = DockStyle.Fill, Minimum = 0, DecimalPlaces = 2 };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White,
        RightToLeft = RightToLeft.Yes,
        RowHeadersVisible = false,
        AllowUserToResizeRows = false
    };

    private readonly Label _balance = new()
    {
        Dock = DockStyle.Bottom,
        Height = 40,
        Font = new Font("Tahoma", 10, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(8)
    };

    private readonly DataTable _lines = new();
    private string? _accountsSnColumn;
    private string? _accountsNameColumn;

    public bool Saved { get; private set; }

    public VoucherEntryForm(
        AppSession session,
        ScreenAccess access,
        VouchersService vouchers)
    {
        _session = session;
        _access = access;
        _vouchers = vouchers;

        ErpTheme.ApplyForm(this);
        Text = "الصقر للمحاسبة — سند جديد";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        MinimizeBox = false;
        MaximizeBox = false;

        BuildLinesTable();
        BuildLayout();
        Shown += async (_, _) => await LoadLookupsAsync();
        _debit.ValueChanged += (_, _) => UpdateBalance();
        _credit.ValueChanged += (_, _) => UpdateBalance();
    }

    private void BuildLinesTable()
    {
        _lines.Columns.Add("AccountSn", typeof(int));
        _lines.Columns.Add("الحساب", typeof(string));
        _lines.Columns.Add("البيان", typeof(string));
        _lines.Columns.Add("مدين", typeof(decimal));
        _lines.Columns.Add("دائن", typeof(decimal));
        _grid.DataSource = _lines;
    }

    private void BuildLayout()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 96,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        var title = new Label
        {
            Text = "سند جديد (قبض / صرف)",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Tahoma", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        var info = new Label
        {
            Text = $"المستخدم: {_session.UserName} | الفرع: {_session.BranchId?.ToString() ?? "-"} — الحفظ عبر الإجراءين الأصليين Insert_Tran_Tran + INSERT_Tran_TranDetails",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(info);
        header.Controls.Add(title);
        Controls.Add(header);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 108,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(8)
        };
        for (var i = 0; i < 4; i++)
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddField(fields, "نوع السند", _typeCombo, 0, 0);
        AddField(fields, "رقم المستند", _docCode, 1, 0);
        AddField(fields, "التاريخ", _date, 2, 0);
        AddField(fields, "ملاحظات", _note, 3, 0);
        Controls.Add(fields);

        var strip = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColumnCount = 5,
            Padding = new Padding(8)
        };
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        AddField(strip, "الحساب", _accountCombo, 0, 0);
        AddField(strip, "البيان", _lineDescription, 1, 0);
        AddField(strip, "مدين", _debit, 2, 0);
        AddField(strip, "دائن", _credit, 3, 0);
        var addLine = new Button
        {
            Text = "إضافة للسند",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat
        };
        addLine.Click += (_, _) => AddLine();
        ErpTheme.ConfigureToolbarButton(addLine, true);
        strip.Controls.Add(addLine, 4, 0);
        Controls.Add(strip);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };
        var save = new Button
        {
            Text = "حفظ السند",
            Width = 130,
            Height = 32,
            Enabled = _access.AllowSave,
            FlatStyle = FlatStyle.Flat
        };
        save.Click += async (_, _) => await SaveAsync();
        ErpTheme.ConfigureToolbarButton(save, true);
        var removeLine = new Button
        {
            Text = "حذف السطر المحدد",
            Width = 130,
            Height = 32,
            FlatStyle = FlatStyle.Flat
        };
        removeLine.Click += (_, _) => RemoveSelectedLine();
        ErpTheme.ConfigureToolbarButton(removeLine);
        var close = new Button { Text = "إغلاق", Width = 100, Height = 32, FlatStyle = FlatStyle.Flat };
        close.Click += (_, _) => Close();
        ErpTheme.ConfigureToolbarButton(close);
        toolbar.Controls.Add(save);
        toolbar.Controls.Add(removeLine);
        toolbar.Controls.Add(close);
        Controls.Add(toolbar);

        Controls.Add(_grid);
        Controls.Add(_balance);
        ErpTheme.ConfigureGrid(_grid);
        UpdateBalance();
    }

    private static void AddField(TableLayoutPanel parent, string label, Control control, int column, int row)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Tahoma", 9, FontStyle.Bold),
            AutoSize = true
        }, 0, 0);
        host.Controls.Add(control, 0, 1);
        parent.Controls.Add(host, column, row);
    }

    private async Task LoadLookupsAsync()
    {
        try
        {
            UseWaitCursor = true;

            var types = await _vouchers.ListTranTypesAsync();
            var idColumn = FindColumn(types, "ID", "id");
            var nameColumn = FindColumn(types, "Name", "name");
            if (idColumn is null || nameColumn is null)
                throw new InvalidOperationException("تعذر التعرف على أعمدة أنواع السندات.");
            _typeCombo.DisplayMember = nameColumn;
            _typeCombo.ValueMember = idColumn;
            _typeCombo.DataSource = types.DefaultView;

            var accounts = await _vouchers.ListAccountsAsync();
            _accountsSnColumn = FindColumn(accounts, "SN", "Sn", "ID", "AccountID", "Account_Sn");
            _accountsNameColumn = FindColumn(accounts, "Account_Name", "Name", "AccountName");
            if (_accountsSnColumn is null || _accountsNameColumn is null)
                throw new InvalidOperationException("تعذر التعرف على أعمدة الحسابات من الإجراء Select_SearchAccount.");
            BindCombo(_accountCombo, accounts, _accountsSnColumn, _accountsNameColumn);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر تحميل البيانات: " + ex.GetBaseException().Message,
                "سند جديد", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static void BindCombo(ComboBox combo, DataTable table, string idColumn, string nameColumn)
    {
        combo.DataSource = null;
        combo.DataSource = new BindingSource(table, null);
        combo.DisplayMember = nameColumn;
        combo.ValueMember = idColumn;

        var names = new AutoCompleteStringCollection();
        foreach (DataRow row in table.Rows)
        {
            var name = row[nameColumn]?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
        combo.AutoCompleteSource = AutoCompleteSource.CustomSource;
        combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        combo.AutoCompleteCustomSource = names;
    }

    private static string? FindColumn(DataTable table, params string[] names)
    {
        foreach (DataColumn column in table.Columns)
            foreach (var name in names)
                if (string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                    return column.ColumnName;
        return null;
    }

    private void AddLine()
    {
        if (!int.TryParse(_accountCombo.SelectedValue?.ToString(), out var accountSn) || accountSn <= 0)
        {
            MessageBox.Show(this, "اختر حساباً صحيحاً أولاً.", "إضافة سطر",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_debit.Value <= 0 && _credit.Value <= 0)
        {
            MessageBox.Show(this, "أدخل مبلغاً مديناً أو دائناً للسطر.", "إضافة سطر",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_debit.Value > 0 && _credit.Value > 0)
        {
            MessageBox.Show(this, "لا يمكن أن يكون السطر مديناً ودائناً معاً.", "إضافة سطر",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _lines.Rows.Add(accountSn, _accountCombo.Text.Trim(), _lineDescription.Text.Trim(),
            _debit.Value, _credit.Value);
        _debit.Value = 0;
        _credit.Value = 0;
        _lineDescription.Text = string.Empty;
        UpdateBalance();
        _accountCombo.Focus();
    }

    private void RemoveSelectedLine()
    {
        if (_grid.CurrentRow is null) return;
        _lines.Rows[_grid.CurrentRow.Index].Delete();
        UpdateBalance();
    }

    private void UpdateBalance()
    {
        var debit = _lines.Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["مدين"]));
        var credit = _lines.Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["دائن"]));
        var difference = debit - credit;
        _balance.Text = difference == 0
            ? $"السند متوازن — إجمالي المدين: {debit:N2} / إجمالي الدائن: {credit:N2}"
            : $"السند غير متوازن — المدين: {debit:N2} / الدائن: {credit:N2} (الفرق: {difference:N2})";
        _balance.ForeColor = difference == 0 ? Color.Green : Color.Firebrick;
    }

    private async Task SaveAsync()
    {
        try
        {
            UseWaitCursor = true;

            if (!int.TryParse(_typeCombo.SelectedValue?.ToString(), out var tranTypeId) || tranTypeId <= 0)
                throw new ArgumentException("اختر نوع السند.");
            if (_lines.Rows.Count == 0)
                throw new ArgumentException("أضف سطراً واحداً على الأقل إلى السند.");

            var voucher = new Voucher
            {
                TranTypeId = tranTypeId,
                DocCode = _docCode.Text,
                VoucherDate = _date.Value,
                Note = _note.Text
            };

            foreach (DataRow line in _lines.Rows)
            {
                voucher.Lines.Add(new VoucherLine
                {
                    AccountSn = Convert.ToInt32(line["AccountSn"]),
                    Description = Convert.ToString(line["البيان"]),
                    Debit = Convert.ToDecimal(line["مدين"]),
                    Credit = Convert.ToDecimal(line["دائن"])
                });
            }

            var serial = await _vouchers.CreateAsync(voucher, _session);
            Saved = true;
            MessageBox.Show(this, $"تم حفظ السند بنجاح — الرقم التسلسلي: {serial}",
                "حفظ السند", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حفظ السند:\r\n" + ex.GetBaseException().Message,
                "حفظ السند", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }
}
