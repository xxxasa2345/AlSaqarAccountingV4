using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class ContractBounceForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public ContractBounceForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "بونص العقود";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListContractBouncesAsync(Session.BranchId);

    protected override bool SupportsDocumentDetails => false;

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => Task.FromResult<DataTable?>(null);
}
