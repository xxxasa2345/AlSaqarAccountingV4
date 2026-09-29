using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

/// <summary>
/// Concrete projects screen (المشاريع). Reads through the original
/// Select_SearchAccountProjects procedure — not a generic catalog screen.
/// </summary>
public sealed class ProjectsForm : BrowseScreenBase
{
    private readonly DocumentsService _service;

    public ProjectsForm(AppSession session, ScreenAccess access, DocumentsService service)
        : base(session, access)
    {
        _service = service;
    }

    protected override string ScreenTitle => "المشاريع";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListProjectsAsync(Session.BranchId);
}
