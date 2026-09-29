using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class PurchaseReturnsForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public PurchaseReturnsForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "مرتجعات المشتريات";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListPurchaseReturnsAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => _service.PrintPurchaseReturnAsync(TryRowId(row, out var id) ? id : 0, Session.BranchId);
}
