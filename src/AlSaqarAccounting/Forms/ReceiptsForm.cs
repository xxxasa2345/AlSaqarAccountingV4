using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Operational vouchers screen (السندات). Lists through the original
/// Select_SearchAccountReceipt procedure, creates vouchers through
/// VoucherEntryForm → Insert_Tran_Tran + INSERT_Tran_TranDetails, and
/// deletes through Delete_Tran_Tran.
/// </summary>
public sealed class ReceiptsForm : BrowseScreenBase
{
    private readonly VouchersService _vouchers;

    public ReceiptsForm(
        AppSession session,
        ScreenAccess access,
        VouchersService vouchers)
        : base(session, access)
    {
        _vouchers = vouchers;
    }

    protected override string ScreenTitle => "السندات";

    protected override Task<DataTable> LoadDataAsync()
        => _vouchers.ListAsync(Session.BranchId);

    protected override void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
        AddButton(toolbar, "سند جديد", Access.AllowSave, async () =>
        {
            using var form = new VoucherEntryForm(Session, Access, _vouchers);
            form.ShowDialog(this);
            if (form.Saved)
                await ReloadAsync();
        });

        AddButton(toolbar, "حذف السند", Access.AllowDelete, async () => await DeleteVoucherAsync());
    }

    private async Task DeleteVoucherAsync()
    {
        var row = CurrentRow;
        var referenceValue = RowValue(row, "ReferenceCode", "ID", "SN", "DocCode");
        var typeValue = RowValue(row, "TranTypeID", "TranType", "Type");
        if (referenceValue is null or DBNull || typeValue is null or DBNull ||
            !int.TryParse(referenceValue.ToString(), out var referenceCode) ||
            !int.TryParse(typeValue.ToString(), out var tranTypeId))
        {
            Status.Text = "تعذر تحديد رقم السند أو نوعه من السجل المحدد.";
            return;
        }

        var confirmation = MessageBox.Show(this,
            $"هل تريد حذف السند رقم {referenceCode}؟\r\n" +
            "سيتم حذف رأس السند وتفاصيله عبر الإجراء الأصلي Delete_Tran_Tran.",
            "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _vouchers.DeleteAsync(referenceCode, Session.BranchId, tranTypeId);
            Status.Text = $"تم حذف السند {referenceCode}.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف السند:\r\n" + ex.GetBaseException().Message,
                "حذف السند", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
