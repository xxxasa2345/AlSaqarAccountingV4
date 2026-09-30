using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational purchase invoices screen (فواتير المشتريات). Lists through the
/// original Select_Order_Purchases procedure, creates invoices through
/// PurchasesEntryForm → Insert_Order_Purchases, and deletes through
/// Delete_Order_Purchases.
/// </summary>
public sealed class PurchasesForm : BrowseScreenBase
{
    private readonly PurchasesService _purchases;
    private readonly StoresService _stores;
    private readonly CustSupService _custSup;

    public PurchasesForm(
        AppSession session,
        ScreenAccess access,
        PurchasesService purchases,
        StoresService stores,
        CustSupService custSup)
        : base(session, access)
    {
        _purchases = purchases;
        _stores = stores;
        _custSup = custSup;
    }

    protected override string ScreenTitle => "فواتير المشتريات";

    protected override Task<DataTable> LoadDataAsync()
        => _purchases.ListAsync(Session.BranchId);

    protected override void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
        AddButton(toolbar, "فاتورة جديدة", Access.AllowSave, async () =>
        {
            using var form = new PurchasesEntryForm(Session, Access, _purchases, _stores, _custSup);
            form.ShowDialog(this);
            if (form.Saved)
                await ReloadAsync();
        });

        AddButton(toolbar, "حذف الفاتورة", Access.AllowDelete, async () => await DeleteInvoiceAsync());
    }

    private async Task DeleteInvoiceAsync()
    {
        var row = CurrentRow;
        if (!TryRowId(row, out var invoiceId))
        {
            Status.Text = "حدد الفاتورة المطلوب حذفها أولاً.";
            return;
        }

        var label = RowValue(row, "SupplierName", "CustSuppName", "Name")?.ToString() ?? string.Empty;
        var confirmation = MessageBox.Show(this,
            $"هل تريد حذف فاتورة المشتريات رقم {invoiceId}؟" +
            (string.IsNullOrWhiteSpace(label) ? string.Empty : $"\r\n{label}") +
            "\r\nسيتم حذف تفاصيلها أيضاً عبر الإجراء الأصلي Delete_Order_Purchases.",
            "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _purchases.DeleteAsync(invoiceId, Session.BranchId);
            Status.Text = $"تم حذف فاتورة المشتريات {invoiceId}.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف الفاتورة:\r\n" + ex.GetBaseException().Message,
                "حذف الفاتورة", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
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
