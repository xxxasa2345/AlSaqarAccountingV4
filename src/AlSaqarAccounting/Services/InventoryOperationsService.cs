using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Operational read services for the inventory/transfer/return/price-offer
/// domains. Every supported list/detail operation uses a confirmed
/// GTSdb2026 stored procedure; inventory-count headers and contract-bounce
/// records use parameterized table reads because the forensic catalog has no
/// branch-scoped SELECT procedure for those headers.
/// </summary>
public sealed class InventoryOperationsService
{
    private readonly DbExecutor _db;

    public InventoryOperationsService(DbExecutor db) => _db = db;

    public Task<DataTable> ListTransfersToBranchAsync(int? branchId, CancellationToken cancellationToken = default)
        => BranchListAsync("dbo.Select_Order_TransferToBranch", branchId, cancellationToken);

    public Task<DataTable> ListTransfersFromBranchAsync(int? branchId, CancellationToken cancellationToken = default)
        => BranchListAsync("dbo.Select_Order_TransferFromBranch", branchId, cancellationToken);

    public Task<DataTable> ListStoreTransfersAsync(int? branchId, CancellationToken cancellationToken = default)
        => BranchListAsync("dbo.Select_Order_StoreTransfer", branchId, cancellationToken);

    public Task<DataTable> ListSalesReturnsAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_Order_OrderReturn", branchId, p =>
        {
            p.Add("@ISNum", SqlDbType.Bit).Value = false;
        }, cancellationToken);

    public Task<DataTable> ListPurchaseReturnsAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_Order_PurchasesReturn", branchId, p =>
        {
            p.Add("@ISNum", SqlDbType.Bit).Value = false;
        }, cancellationToken);

    public Task<DataTable> ListPriceOffersAsync(int? branchId, CancellationToken cancellationToken = default)
        => BranchListAsync("dbo.Select_Order_PriceOffer", branchId, cancellationToken);

    public Task<DataTable> ListGuaranteesAsync(int? branchId, CancellationToken cancellationToken = default)
        => BranchListAsync("dbo.Select_Contract_Guarantee", branchId, cancellationToken);

    public Task<DataTable> ListInventoryStockAsync(CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync("dbo.Get_Items_Stock_Report", cancellationToken: cancellationToken);

    public Task<DataTable> ListInventoryCountsAsync(int? branchId, CancellationToken cancellationToken = default)
    {
        if (!branchId.HasValue)
            throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");

        return _db.QueryAsync(
            @"
SELECT ID,
       BranchID,
       DateGard,
       NoteNum,
       Note,
       UserID_Add,
       UserDate_Add,
       UserID_Update,
       UserDate_Update
FROM dbo.Order_Gard
WHERE BranchID = @BranchID
ORDER BY ID DESC;",
            p => p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value,
            cancellationToken);
    }

    public Task<DataTable> ListContractBouncesAsync(int? branchId, CancellationToken cancellationToken = default)
    {
        if (!branchId.HasValue)
            throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");

        return _db.QueryAsync(
            @"
SELECT SN,
       ID,
       Name,
       BranchID,
       profit,
       ISProfitOrder,
       Phone,
       PlaceID,
       PlateNumber,
       UserID_Add,
       UserDate_Add,
       UserID_Update,
       UserDate_Update
FROM dbo.Contract_Bounce
WHERE BranchID = @BranchID
ORDER BY SN DESC;",
            p => p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value,
            cancellationToken);
    }

    public Task<DataTable> PrintTransferToBranchAsync(int id, int? branchId, CancellationToken cancellationToken = default)
        => DocumentPrintAsync("dbo.Print_Order_TransferToBranch", id, branchId, cancellationToken);

    public Task<DataTable> PrintTransferFromBranchAsync(int id, int? branchId, CancellationToken cancellationToken = default)
        => DocumentPrintAsync("dbo.Print_Order_TransferFromBranch", id, branchId, cancellationToken);

    public Task<DataTable> PrintStoreTransferAsync(int id, int? branchId, CancellationToken cancellationToken = default)
        => DocumentPrintAsync("dbo.Print_Order_StoreTransfer", id, branchId, cancellationToken);

    public Task<DataTable> PrintSalesReturnAsync(int id, int? branchId, int creditNote, CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.Print_Order_OrderReturn",
            p =>
            {
                p.Add("@ID", SqlDbType.Int).Value = id;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
                p.Add("@CreditNote", SqlDbType.Int).Value = creditNote;
            },
            cancellationToken);

    public Task<DataTable> PrintPurchaseReturnAsync(int id, int? branchId, CancellationToken cancellationToken = default)
        => DocumentPrintAsync("dbo.Print_Order_PurchasesReturn", id, branchId, cancellationToken);

    public Task<DataTable> GetPriceOfferDetailsAsync(int id, int? branchId, CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.GetPriceOffersWithDetails",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = id;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
            },
            cancellationToken);

    public Task<DataTable> PrintPriceOfferAsync(int id, int? branchId, CancellationToken cancellationToken = default)
        => DocumentPrintAsync("dbo.Print_Order_PriceOffer", id, branchId, cancellationToken);

    public Task<DataTable> GetGuaranteeDetailsAsync(int id, int? branchId, CancellationToken cancellationToken = default)
    {
        if (!branchId.HasValue)
            throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");

        return _db.QueryAsync(
            @"
SELECT SN,
       Purchese_ID,
       ItemID,
       BranchID,
       ItemUnitID,
       Quantity,
       LastCost,
       SmallUnitPrice,
       UnitPrice,
       TotalPrice,
       VAT,
       NetUnitPrice,
       NetTotalPrice,
       VAT_Discount,
       ItemUnitType,
       ItemDate,
       QuantityExpire,
       ItemDateExpire
FROM dbo.Contract_GuaranteeDetails
WHERE Purchese_ID = @GuaranteeID
  AND BranchID = @BranchID
ORDER BY SN;",
            p =>
            {
                p.Add("@GuaranteeID", SqlDbType.Int).Value = id;
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            },
            cancellationToken);
    }

    public Task<DataTable> ListInventorySettlementMinusAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync(
            "dbo.Select_Order_InventorySettlementMinus",
            branchId,
            null,
            cancellationToken);

    public Task<DataTable> PrintInventorySettlementMinusAsync(
        int id,
        int? branchId,
        CancellationToken cancellationToken = default)
        => DocumentPrintAsync(
            "dbo.Print_Order_InventorySettlementMinus",
            id,
            branchId,
            cancellationToken);

    public Task<DataTable> GetInventoryCountDetailsAsync(int id, CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.Select_OrderGard",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);

    private Task<DataTable> BranchListAsync(
        string procedureName,
        int? branchId,
        CancellationToken cancellationToken)
        => ExecuteBranchProcedureAsync(procedureName, branchId, null, cancellationToken);

    private Task<DataTable> ExecuteBranchProcedureAsync(
        string procedureName,
        int? branchId,
        Action<SqlParameterCollection>? extraParameters,
        CancellationToken cancellationToken)
    {
        return _db.ExecuteStoredProcedureAsync(
            procedureName,
            p =>
            {
                if (!branchId.HasValue)
                    throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");

                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
                extraParameters?.Invoke(p);
            },
            cancellationToken);
    }

    private Task<DataTable> DocumentPrintAsync(
        string procedureName,
        int id,
        int? branchId,
        CancellationToken cancellationToken)
        => _db.ExecuteStoredProcedureAsync(
            procedureName,
            p =>
            {
                p.Add("@ID", SqlDbType.Int).Value = id;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
            },
            cancellationToken);
}
