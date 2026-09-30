using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational customers screen (العملاء). Lists through the original
/// Select_AccountCustomer procedure. Adding a customer creates a real GL
/// account under the default-customer parent (Account_DefualtCustomer) and
/// registers the customer row, exactly like the original system.
/// </summary>
public sealed class CustomersForm : BrowseScreenBase
{
    private readonly CustSupService _custSup;
    private readonly AccountsTreeService _accounts;

    public CustomersForm(
        AppSession session,
        ScreenAccess access,
        CustSupService custSup,
        AccountsTreeService accounts)
        : base(session, access)
    {
        _custSup = custSup;
        _accounts = accounts;
    }

    protected override string ScreenTitle => "العملاء";

    protected override Task<DataTable> LoadDataAsync()
        => _custSup.ListCustomersAsync(Session.BranchId);

    protected override void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
        AddButton(toolbar, "إضافة عميل", Access.AllowSave, async () => await AddCustomerAsync());
        AddButton(toolbar, "حذف العميل", Access.AllowDelete, async () => await DeleteCustomerAsync());
    }

    private async Task AddCustomerAsync()
    {
        if (!ShowCustomerDialog(this, out var name, out var phone, out var vatNumber))
            return;

        try
        {
            UseWaitCursor = true;

            var parentAccountNo = await _accounts.GetDefaultCustomerParentAsync(Session.BranchId);
            var accountNo = await _accounts.CreateAccountAsync(parentAccountNo, name, Session);
            await _custSup.RegisterCustomerAsync(name, accountNo, phone, vatNumber, Session);

            Status.Text = $"تمت إضافة العميل \"{name}\" تحت الحساب رقم {accountNo}.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر إضافة العميل:\r\n" + ex.GetBaseException().Message,
                "إضافة عميل", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task DeleteCustomerAsync()
    {
        var row = CurrentRow;
        var snValue = RowValue(row, "SN", "ID", "Sn");
        if (snValue is null or DBNull || !int.TryParse(snValue.ToString(), out var sn) || sn <= 0)
        {
            Status.Text = "تعذر تحديد العميل المطلوب حذفه.";
            return;
        }

        var name = RowValue(row, "Name", "CustSuppName", "Account_Name")?.ToString() ?? string.Empty;
        var confirmation = MessageBox.Show(this,
            $"هل تريد حذف تسجيل العميل؟\r\n{name}\r\n(حسابه في شجرة الحسابات لن يُحذف).",
            "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _custSup.DeleteCustomerAsync(sn);
            Status.Text = "تم حذف تسجيل العميل.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف العميل:\r\n" + ex.GetBaseException().Message,
                "حذف العميل", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    /// <summary>Small modal editor collecting the new customer's fields.</summary>
    private static bool ShowCustomerDialog(
        IWin32Window owner,
        out string name,
        out string phone,
        out string vatNumber)
    {
        name = string.Empty;
        phone = string.Empty;
        vatNumber = string.Empty;

        using var dialog = new Form
        {
            Text = "إضافة عميل جديد",
            Width = 460,
            Height = 230,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };

        var nameBox = new TextBox { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
        var phoneBox = new TextBox { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
        var vatBox = new TextBox { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 110,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        fields.Controls.Add(new Label
        {
            Text = "اسم العميل:",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        }, 0, 0);
        fields.Controls.Add(nameBox, 1, 0);
        fields.Controls.Add(new Label
        {
            Text = "الهاتف:",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        }, 0, 1);
        fields.Controls.Add(phoneBox, 1, 1);
        fields.Controls.Add(new Label
        {
            Text = "الرقم الضريبي:",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        }, 0, 2);
        fields.Controls.Add(vatBox, 1, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var ok = new Button { Text = "حفظ", Width = 90, Height = 30 };
        var cancel = new Button { Text = "إلغاء", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        dialog.Controls.Add(fields);
        dialog.Controls.Add(buttons);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show(dialog, "اسم العميل مطلوب.", "إضافة عميل",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            dialog.DialogResult = DialogResult.OK;
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
            return false;

        name = nameBox.Text.Trim();
        phone = phoneBox.Text.Trim();
        vatNumber = vatBox.Text.Trim();
        return true;
    }

    private static void AddButton(FlowLayoutPanel toolbar, string text, bool enabled, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            Width = 115,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat
        };
        button.Click += async (_, _) => await action();
        toolbar.Controls.Add(button);
    }
}
