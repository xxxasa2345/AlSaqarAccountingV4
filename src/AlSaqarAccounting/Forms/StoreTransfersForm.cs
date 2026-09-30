using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class StoreTransfersForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public StoreTransfersForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "تحويلات المخازن";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListStoreTransfersAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => _service.PrintStoreTransferAsync(TryRowId(row, out var id) ? id : 0, Session.BranchId);
}
