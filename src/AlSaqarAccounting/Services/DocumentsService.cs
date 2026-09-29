using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real read services for operational documents. Every list goes through the
/// original GTSdb2026 procedure confirmed in the procedure catalog. Write
/// operations stay disabled until each screen's original write procedure
/// contract is extracted, exactly as documented in Phase 5.
/// </summary>
public sealed class DocumentsService
{
    private readonly DbExecutor _db;

    public DocumentsService(DbExecutor db) => _db = db;

    public Task<DataTable> ListOrdersAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_Order_Orders", branchId, cancellationToken);

    public Task<DataTable> ListPurchasesAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_Order_Purchases", branchId, cancellationToken);

    public Task<DataTable> ListReceiptsAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_SearchAccountReceipt", branchId, cancellationToken);

    public Task<DataTable> ListCostCentersAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_SearchAccountCostCenter", branchId, cancellationToken);

    public Task<DataTable> ListProjectsAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_SearchAccountProjects", branchId, cancellationToken);

    private Task<DataTable> ExecuteBranchProcedureAsync(
        string procedureName,
        int? branchId,
        CancellationToken cancellationToken)
    {
        return _db.ExecuteStoredProcedureAsync(
            procedureName,
            p =>
            {
                if (!branchId.HasValue)
                    throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            },
            cancellationToken);
    }
}
