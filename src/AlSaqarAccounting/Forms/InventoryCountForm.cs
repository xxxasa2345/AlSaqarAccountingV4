using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class InventoryCountForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public InventoryCountForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "الجرد";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListInventoryCountsAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => _service.GetInventoryCountDetailsAsync(TryRowId(row, out var id) ? id : 0);
}
