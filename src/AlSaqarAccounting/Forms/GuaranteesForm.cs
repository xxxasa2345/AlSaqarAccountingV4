using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class GuaranteesForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public GuaranteesForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "ضمانات العقود";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListGuaranteesAsync(Session.BranchId);

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
    {
        var id = TryRowId(row, out var guaranteeId) ? guaranteeId : 0;
        return _service.GetGuaranteeDetailsAsync(guaranteeId, Session.BranchId);
    }
}
