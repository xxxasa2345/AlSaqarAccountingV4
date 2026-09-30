using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class OpenQuantitiesForm : ProcedureDocumentBrowseForm
{
    private readonly OpenQuantityService _service;

    public OpenQuantitiesForm(AppSession session, ScreenAccess access, OpenQuantityService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "الكميات الافتتاحية";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => _service.PrintAsync(TryRowId(row, out var id) ? id : 0, Session.BranchId);

    protected override void AddToolbarButtons(FlowLayoutPanel toolbar)
    {
        base.AddToolbarButtons(toolbar);

        AddAsyncButton(toolbar, "كمية افتتاحية جديدة", Access.AllowSave, async () =>
        {
            using var form = new OpenQuantityEntryForm(
                Session,
                Access,
                _service);

            form.ShowDialog(this);
            if (form.Saved)
                await ReloadAsync();
        }, 145);

        AddAsyncButton(toolbar, "تعديل", Access.AllowEdit, async () =>
        {
            if (!TryRowId(CurrentRow, out var id))
            {
                Status.Text = "حدد مستنداً للتعديل أولاً.";
                return;
            }

            using var form = new OpenQuantityEntryForm(
                Session,
                Access,
                _service,
                id);

            form.ShowDialog(this);
            if (form.Saved)
                await ReloadAsync();
        });

        AddAsyncButton(toolbar, "حذف", Access.AllowDelete, DeleteCurrentAsync);

    }

    private async Task DeleteCurrentAsync()
    {
        if (!TryRowId(CurrentRow, out var id))
        {
            Status.Text = "حدد مستنداً للحذف أولاً.";
            return;
        }

        if (MessageBox.Show(
                this,
                $"هل تريد حذف الكمية الافتتاحية رقم {id}؟",
                "تأكيد الحذف",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            await _service.DeleteAsync(id, Session.BranchId);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "تعذر حذف المستند:
" + ex.GetBaseException().Message,
                "الكميات الافتتاحية", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }
}
