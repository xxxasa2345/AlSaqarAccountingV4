using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class TransferFromBranchForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public TransferFromBranchForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "التحويل من فرع";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListTransfersFromBranchAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => _service.PrintTransferFromBranchAsync(TryRowId(row, out var id) ? id : 0, Session.BranchId);
}
