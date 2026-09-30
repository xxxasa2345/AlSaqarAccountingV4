using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete cost centers screen (مراكز التكلفة). Reads through the original
/// Select_SearchAccountCostCenter procedure — not a generic catalog screen.
/// </summary>
public sealed class CostCentersForm : BrowseScreenBase
{
    private readonly DocumentsService _service;

    public CostCentersForm(AppSession session, ScreenAccess access, DocumentsService service)
        : base(session, access)
    {
        _service = service;
    }

    protected override string ScreenTitle => "مراكز التكلفة";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListCostCentersAsync(Session.BranchId);
}
