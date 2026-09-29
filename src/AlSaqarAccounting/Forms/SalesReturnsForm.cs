using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class SalesReturnsForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public SalesReturnsForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "مرتجعات المبيعات";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListSalesReturnsAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
    {
        var id = TryRowId(row, out var documentId) ? documentId : 0;
        var creditNoteValue = RowValue(row, "CreditNote");
        var creditNote = 0;
        if (creditNoteValue is not null && creditNoteValue != DBNull.Value)
        {
            try { creditNote = Convert.ToInt32(creditNoteValue); }
            catch { creditNote = 0; }
        }

        return _service.PrintSalesReturnAsync(id, Session.BranchId, creditNote);
    }
}
