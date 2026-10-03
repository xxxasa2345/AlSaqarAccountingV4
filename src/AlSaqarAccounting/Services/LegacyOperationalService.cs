using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Read-side bindings for legacy ERP modules whose original GTSdb procedures
/// are already present in the repository procedure catalog. This keeps the
/// navigation connected to real database operations instead of opening a
/// generic placeholder form. Write contracts remain owned by the original
/// module services until their exact parameter/TVP contracts are migrated.
/// </summary>
public sealed class LegacyOperationalService
{
    private readonly DbExecutor _db;

    public LegacyOperationalService(DbExecutor db) => _db = db;

    public Task<DataTable> ListBranchAsync(
        string procedureName,
        int? branchId,
        CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            procedureName,
            p =>
            {
                if (!branchId.HasValue)
                    throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");

                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            },
            cancellationToken);

    public Task<DataTable> ListCheckoutAsync(
        int? branchId,
        CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.Select_Order_CheckoutOrders",
            p =>
            {
                if (!branchId.HasValue)
                    throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");

                p.Add("@ISNum", SqlDbType.Bit).Value = false;
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            },
            cancellationToken);

    public Task<DataTable> ListRestaurantDeliveryAsync(
        int? branchId,
        CancellationToken cancellationToken = default)
        => ListBranchAsync("dbo.Get_Restaurant_Delivery", branchId, cancellationToken);

    public Task<DataTable> GetRepairDetailsAsync(
        int repairId,
        CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.GetRepairWithDetails",
            p => p.Add("@RepairId", SqlDbType.Int).Value = repairId,
            cancellationToken);

    public Task<DataTable> GetContractCustomerReportAsync(
        int branchId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.GetReport_Contract",
            p =>
            {
                p.Add("@BranchID", SqlDbType.Int).Value = branchId;
                p.Add("@Date1", SqlDbType.DateTime).Value = fromDate;
                p.Add("@Date2", SqlDbType.DateTime).Value = toDate;
            },
            cancellationToken);

    public Task<DataTable> GetDailyMovementsAsync(
        int branchId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.GetReport_MovementsDailyReport",
            p =>
            {
                p.Add("@BranchID", SqlDbType.Int).Value = branchId;
                p.Add("@Date1", SqlDbType.DateTime).Value = fromDate;
                p.Add("@Date2", SqlDbType.DateTime).Value = toDate;
            },
            cancellationToken);
}
