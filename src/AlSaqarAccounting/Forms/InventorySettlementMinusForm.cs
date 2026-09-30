using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class InventorySettlementMinusForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public InventorySettlementMinusForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "تسوية جرد بالنقص";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListInventorySettlementMinusAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => _service.PrintInventorySettlementMinusAsync(
            TryRowId(row, out var id) ? id : 0,
            Session.BranchId);
}
