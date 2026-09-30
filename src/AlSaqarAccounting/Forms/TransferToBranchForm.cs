using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class TransferToBranchForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public TransferToBranchForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "التحويل إلى فرع";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListTransfersToBranchAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => _service.PrintTransferToBranchAsync(TryRowId(row, out var id) ? id : 0, Session.BranchId);
}
