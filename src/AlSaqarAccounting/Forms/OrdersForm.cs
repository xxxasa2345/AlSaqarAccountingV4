using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational sales invoices screen (فواتير المبيعات). Lists through the
/// original Select_Order_Orders procedure, creates invoices through
/// SalesEntryForm → Insert_Order_Order_ALL, and deletes through
/// Delete_Order_Orders.
/// </summary>
public sealed class OrdersForm : BrowseScreenBase
{
    private readonly SalesService _sales;
    private readonly StoresService _stores;

    public OrdersForm(
        AppSession session,
        ScreenAccess access,
        SalesService sales,
        StoresService stores)
        : base(session, access)
    {
        _sales = sales;
        _stores = stores;
    }

    protected override string ScreenTitle => "فواتير المبيعات";

    protected override Task<DataTable> LoadDataAsync()
        => _sales.ListAsync(Session.BranchId);

    protected override void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
        AddButton(toolbar, "فاتورة جديدة", Access.AllowSave, async () =>
        {
            using var form = new RealSalesInvoiceFormFixed(Session, Access, _sales, _stores);
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

        var label = RowValue(row, "SupplierName", "Name", "CustSuppName")?.ToString() ?? string.Empty;
        var confirmation = MessageBox.Show(this,
            $"هل تريد حذف الفاتورة رقم {invoiceId}؟" +
            (string.IsNullOrWhiteSpace(label) ? string.Empty : $"\r\n{label}") +
            "\r\nسيتم حذف تفاصيلها أيضاً عبر الإجراء الأصلي Delete_Order_Orders.",
            "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _sales.DeleteAsync(invoiceId, Session.BranchId);
            Status.Text = $"تم حذف الفاتورة {invoiceId}.";
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
