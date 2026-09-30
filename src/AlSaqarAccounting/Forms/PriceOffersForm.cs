using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class PriceOffersForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public PriceOffersForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "عروض الأسعار";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListPriceOffersAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
    {
        var id = TryRowId(row, out var offerId) ? offerId : 0;
        return _service.GetPriceOfferDetailsAsync(id, Session.BranchId);
    }
}
