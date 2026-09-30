using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Forms;

public sealed class InventoryStockForm : ProcedureDocumentBrowseForm
{
    private readonly InventoryOperationsService _service;

    public InventoryStockForm(AppSession session, ScreenAccess access, InventoryOperationsService service)
        : base(session, access) => _service = service;

    protected override string ScreenTitle => "كميات المخزون";

    protected override Task<DataTable> LoadDataAsync()
        => _service.ListInventoryStockAsync();

    protected override bool SupportsDocumentDetails => false;

    protected override Task<DataTable?> LoadDocumentDetailsAsync(DataRowView row)
        => Task.FromResult<DataTable?>(null);
}
