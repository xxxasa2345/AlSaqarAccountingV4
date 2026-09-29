using System.Data;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Cashier operations (Point of Sale).
/// Handles sales, receipts, and daily cashier operations.
/// </summary>
public sealed class CashierService
{
    private readonly DbExecutor _db;

    public CashierService(DbExecutor db) => _db = db;

    #region Sales Operations

    /// <summary>
    /// Create a new sale order (POS transaction)
    /// </summary>
    public async Task<int> CreateSaleAsync(
        Order_Orders order,
        List<Order_OrdersDetails> details,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        // Validate order
        if (details == null || details.Count == 0)
            throw new ArgumentException("Order must have at least one item");
        
        if (string.IsNullOrWhiteSpace(order.SupplierName))
            throw new ArgumentException("Customer name is required");

        // Start transaction
        using var cn = new SqlConnection(_db.ConnectionString);
        await cn.OpenAsync(cancellationToken);
        
        using var transaction = cn.BeginTransaction();
        
        try
        {
            // Insert order header
            order.OrderCashierType = true;
            order.BranchID = session.BranchId;
            order.UserID_Add = session.UserId;
            order.UserBranch_Add = session.BranchId;
            order.UserMacAddress_Add = GetMachineMac();
            order.UserDate_Add = DateTime.Now;
            order.Purchases_Date = DateTime.Now;

            const string orderSql = @"
INSERT INTO dbo.Order_Orders (
    PurBranchID, BranchID, SupplierName, SupplierPhone, SupplierVatNum,
    Purchases_Date, Note, NoteNum, CostOrder, Tax, TotalPrices, Safy,
    DiscountNum, DiscountPerantage, Tax_Discount, TotalPrices_Discount, AllTax, Net,
    CashMoney, CashBank, OrderCashierType, RoomNum, TableNum,
    UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add,
    BuildingNum, Street, District, City, Country, PostalCode, AdditionalNum, CommercialRecord
)
VALUES (
    @PurBranchID, @BranchID, @SupplierName, @SupplierPhone, @SupplierVatNum,
    @Purchases_Date, @Note, @NoteNum, @CostOrder, @Tax, @TotalPrices, @Safy,
    @DiscountNum, @DiscountPerantage, @Tax_Discount, @TotalPrices_Discount, @AllTax, @Net,
    @CashMoney, @CashBank, @OrderCashierType, @RoomNum, @TableNum,
    @UserID_Add, @UserBranch_Add, @UserMacAddress_Add, @UserDate_Add,
    @BuildingNum, @Street, @District, @City, @Country, @PostalCode, @AdditionalNum, @CommercialRecord
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var orderCmd = new SqlCommand(orderSql, cn, transaction);
            orderCmd.Parameters.Add("@PurBranchID", SqlDbType.Int).Value = (object)order.PurBranchID ?? DBNull.Value;
            orderCmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = (object)order.BranchID ?? DBNull.Value;
            orderCmd.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 300).Value = order.SupplierName.Trim();
            orderCmd.Parameters.Add("@SupplierPhone", SqlDbType.NVarChar, 100).Value = (object)order.SupplierPhone ?? DBNull.Value;
            orderCmd.Parameters.Add("@SupplierVatNum", SqlDbType.NVarChar, 100).Value = (object)order.SupplierVatNum ?? DBNull.Value;
            orderCmd.Parameters.Add("@Purchases_Date", SqlDbType.DateTime).Value = order.Purchases_Date;
            orderCmd.Parameters.Add("@Note", SqlDbType.NVarChar, 1000).Value = (object)order.Note ?? DBNull.Value;
            orderCmd.Parameters.Add("@NoteNum", SqlDbType.NVarChar, 100).Value = (object)order.NoteNum ?? DBNull.Value;
            orderCmd.Parameters.Add("@CostOrder", SqlDbType.Decimal).Value = (object)order.CostOrder ?? DBNull.Value;
            orderCmd.Parameters.Add("@Tax", SqlDbType.Decimal).Value = (object)order.Tax ?? DBNull.Value;
            orderCmd.Parameters.Add("@TotalPrices", SqlDbType.Decimal).Value = (object)order.TotalPrices ?? DBNull.Value;
            orderCmd.Parameters.Add("@Safy", SqlDbType.Decimal).Value = (object)order.Safy ?? DBNull.Value;
            orderCmd.Parameters.Add("@DiscountNum", SqlDbType.Decimal).Value = (object)order.DiscountNum ?? DBNull.Value;
            orderCmd.Parameters.Add("@DiscountPerantage", SqlDbType.Decimal).Value = (object)order.DiscountPerantage ?? DBNull.Value;
            orderCmd.Parameters.Add("@Tax_Discount", SqlDbType.Decimal).Value = (object)order.Tax_Discount ?? DBNull.Value;
            orderCmd.Parameters.Add("@TotalPrices_Discount", SqlDbType.Decimal).Value = (object)order.TotalPrices_Discount ?? DBNull.Value;
            orderCmd.Parameters.Add("@AllTax", SqlDbType.Decimal).Value = (object)order.AllTax ?? DBNull.Value;
            orderCmd.Parameters.Add("@Net", SqlDbType.Decimal).Value = (object)order.Net ?? DBNull.Value;
            orderCmd.Parameters.Add("@CashMoney", SqlDbType.Decimal).Value = (object)order.CashMoney ?? DBNull.Value;
            orderCmd.Parameters.Add("@CashBank", SqlDbType.Decimal).Value = (object)order.CashBank ?? DBNull.Value;
            orderCmd.Parameters.Add("@OrderCashierType", SqlDbType.Bit).Value = order.OrderCashierType;
            orderCmd.Parameters.Add("@RoomNum", SqlDbType.Int).Value = (object)order.RoomNum ?? DBNull.Value;
            orderCmd.Parameters.Add("@TableNum", SqlDbType.Int).Value = (object)order.TableNum ?? DBNull.Value;
            orderCmd.Parameters.Add("@UserID_Add", SqlDbType.Int).Value = order.UserID_Add;
            orderCmd.Parameters.Add("@UserBranch_Add", SqlDbType.Int).Value = (object)order.UserBranch_Add ?? DBNull.Value;
            orderCmd.Parameters.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = order.UserMacAddress_Add;
            orderCmd.Parameters.Add("@UserDate_Add", SqlDbType.DateTime).Value = order.UserDate_Add;
            orderCmd.Parameters.Add("@BuildingNum", SqlDbType.NVarChar, 100).Value = (object)order.BuildingNum ?? DBNull.Value;
            orderCmd.Parameters.Add("@Street", SqlDbType.NVarChar, 200).Value = (object)order.Street ?? DBNull.Value;
            orderCmd.Parameters.Add("@District", SqlDbType.NVarChar, 200).Value = (object)order.District ?? DBNull.Value;
            orderCmd.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = (object)order.City ?? DBNull.Value;
            orderCmd.Parameters.Add("@Country", SqlDbType.NVarChar, 100).Value = (object)order.Country ?? DBNull.Value;
            orderCmd.Parameters.Add("@PostalCode", SqlDbType.NVarChar, 50).Value = (object)order.PostalCode ?? DBNull.Value;
            orderCmd.Parameters.Add("@AdditionalNum", SqlDbType.NVarChar, 100).Value = (object)order.AdditionalNum ?? DBNull.Value;
            orderCmd.Parameters.Add("@CommercialRecord", SqlDbType.NVarChar, 100).Value = (object)order.CommercialRecord ?? DBNull.Value;

            var orderId = Convert.ToInt32(await orderCmd.ExecuteScalarAsync(cancellationToken));

            // Insert order details
            foreach (var detail in details)
            {
                detail.Purchese_ID = orderId;
                detail.BranchID = session.BranchId;

                const string detailSql = @"
INSERT INTO dbo.Order_OrdersDetails (
    Purchese_ID, R_ItmSN, R_RowId, R_ADDId, ItemID, ItemIDADD, BranchID, IsWaiting,
    StoreID, ItemUnitID, Quantity, LastCost, SmallUnitPrice, UnitPrice, TotalPrice,
    VAT, NetUnitPrice, NetTotalPrice, VAT_Discount, ItemUnitType, IsPrint,
    ItemNote, IsPrintCook, UnitNumber, Weight, Height, DetailsData, width, Long, Bounce,
    Amount_Discount, Per_Discount, TafqitUnitId, TafqitQunitity, TobaccoTax, TobaccoTaxDis,
    TotalWithTaxTobacco, PriceInstall, PriceInstallDise
)
VALUES (
    @Purchese_ID, @R_ItmSN, @R_RowId, @R_ADDId, @ItemID, @ItemIDADD, @BranchID, @IsWaiting,
    @StoreID, @ItemUnitID, @Quantity, @LastCost, @SmallUnitPrice, @UnitPrice, @TotalPrice,
    @VAT, @NetUnitPrice, @NetTotalPrice, @VAT_Discount, @ItemUnitType, @IsPrint,
    @ItemNote, @IsPrintCook, @UnitNumber, @Weight, @Height, @DetailsData, @width, @Long, @Bounce,
    @Amount_Discount, @Per_Discount, @TafqitUnitId, @TafqitQunitity, @TobaccoTax, @TobaccoTaxDis,
    @TotalWithTaxTobacco, @PriceInstall, @PriceInstallDise
);";

                using var detailCmd = new SqlCommand(detailSql, cn, transaction);
                detailCmd.Parameters.Add("@Purchese_ID", SqlDbType.Int).Value = detail.Purchese_ID;
                detailCmd.Parameters.Add("@R_ItmSN", SqlDbType.Int).Value = (object)detail.R_ItmSN ?? DBNull.Value;
                detailCmd.Parameters.Add("@R_RowId", SqlDbType.Int).Value = (object)detail.R_RowId ?? DBNull.Value;
                detailCmd.Parameters.Add("@R_ADDId", SqlDbType.Int).Value = (object)detail.R_ADDId ?? DBNull.Value;
                detailCmd.Parameters.Add("@ItemID", SqlDbType.Int).Value = (object)detail.ItemID ?? DBNull.Value;
                detailCmd.Parameters.Add("@ItemIDADD", SqlDbType.Int).Value = (object)detail.ItemIDADD ?? DBNull.Value;
                detailCmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = (object)detail.BranchID ?? DBNull.Value;
                detailCmd.Parameters.Add("@IsWaiting", SqlDbType.Bit).Value = (object)detail.IsWaiting ?? DBNull.Value;
                detailCmd.Parameters.Add("@StoreID", SqlDbType.Int).Value = (object)detail.StoreID ?? DBNull.Value;
                detailCmd.Parameters.Add("@ItemUnitID", SqlDbType.Int).Value = (object)detail.ItemUnitID ?? DBNull.Value;
                detailCmd.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = (object)detail.Quantity ?? DBNull.Value;
                detailCmd.Parameters.Add("@LastCost", SqlDbType.Decimal).Value = (object)detail.LastCost ?? DBNull.Value;
                detailCmd.Parameters.Add("@SmallUnitPrice", SqlDbType.Decimal).Value = (object)detail.SmallUnitPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@UnitPrice", SqlDbType.Decimal).Value = (object)detail.UnitPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@TotalPrice", SqlDbType.Decimal).Value = (object)detail.TotalPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@VAT", SqlDbType.Decimal).Value = (object)detail.VAT ?? DBNull.Value;
                detailCmd.Parameters.Add("@NetUnitPrice", SqlDbType.Decimal).Value = (object)detail.NetUnitPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@NetTotalPrice", SqlDbType.Decimal).Value = (object)detail.NetTotalPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@VAT_Discount", SqlDbType.Decimal).Value = (object)detail.VAT_Discount ?? DBNull.Value;
                detailCmd.Parameters.Add("@ItemUnitType", SqlDbType.NVarChar, 100).Value = (object)detail.ItemUnitType ?? DBNull.Value;
                detailCmd.Parameters.Add("@IsPrint", SqlDbType.Bit).Value = (object)detail.IsPrint ?? DBNull.Value;
                detailCmd.Parameters.Add("@ItemNote", SqlDbType.NVarChar, 500).Value = (object)detail.ItemNote ?? DBNull.Value;
                detailCmd.Parameters.Add("@IsPrintCook", SqlDbType.Bit).Value = (object)detail.IsPrintCook ?? DBNull.Value;
                detailCmd.Parameters.Add("@UnitNumber", SqlDbType.Decimal).Value = (object)detail.UnitNumber ?? DBNull.Value;
                detailCmd.Parameters.Add("@Weight", SqlDbType.Decimal).Value = (object)detail.Weight ?? DBNull.Value;
                detailCmd.Parameters.Add("@Height", SqlDbType.Decimal).Value = (object)detail.Height ?? DBNull.Value;
                detailCmd.Parameters.Add("@DetailsData", SqlDbType.NVarChar).Value = (object)detail.DetailsData ?? DBNull.Value;
                detailCmd.Parameters.Add("@width", SqlDbType.Decimal).Value = (object)detail.width ?? DBNull.Value;
                detailCmd.Parameters.Add("@Long", SqlDbType.Decimal).Value = (object)detail.Long ?? DBNull.Value;
                detailCmd.Parameters.Add("@Bounce", SqlDbType.Decimal).Value = (object)detail.Bounce ?? DBNull.Value;
                detailCmd.Parameters.Add("@Amount_Discount", SqlDbType.Decimal).Value = (object)detail.Amount_Discount ?? DBNull.Value;
                detailCmd.Parameters.Add("@Per_Discount", SqlDbType.Decimal).Value = (object)detail.Per_Discount ?? DBNull.Value;
                detailCmd.Parameters.Add("@TafqitUnitId", SqlDbType.Int).Value = (object)detail.TafqitUnitId ?? DBNull.Value;
                detailCmd.Parameters.Add("@TafqitQunitity", SqlDbType.Decimal).Value = (object)detail.TafqitQunitity ?? DBNull.Value;
                detailCmd.Parameters.Add("@TobaccoTax", SqlDbType.Decimal).Value = (object)detail.TobaccoTax ?? DBNull.Value;
                detailCmd.Parameters.Add("@TobaccoTaxDis", SqlDbType.Decimal).Value = (object)detail.TobaccoTaxDis ?? DBNull.Value;
                detailCmd.Parameters.Add("@TotalWithTaxTobacco", SqlDbType.Decimal).Value = (object)detail.TotalWithTaxTobacco ?? DBNull.Value;
                detailCmd.Parameters.Add("@PriceInstall", SqlDbType.Decimal).Value = (object)detail.PriceInstall ?? DBNull.Value;
                detailCmd.Parameters.Add("@PriceInstallDise", SqlDbType.Decimal).Value = (object)detail.PriceInstallDise ?? DBNull.Value;

                await detailCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
            return orderId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Get list of sales for current cashier
    /// </summary>
    public Task<DataTable> GetSalesAsync(int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var sql = @"SELECT * FROM dbo.Order_Orders 
                   WHERE OrderCashierType = 1";

        if (branchId.HasValue)
            sql += " AND (BranchID = @BranchID OR BranchID IS NULL)";
        
        if (fromDate.HasValue)
            sql += " AND Purchases_Date >= @FromDate";
        
        if (toDate.HasValue)
            sql += " AND Purchases_Date <= @ToDate";
        
        sql += " ORDER BY Purchases_Date DESC";

        return _db.QueryAsync(sql, p =>
        {
            if (branchId.HasValue)
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            if (fromDate.HasValue)
                p.Add("@FromDate", SqlDbType.Date).Value = fromDate.Value;
            if (toDate.HasValue)
                p.Add("@ToDate", SqlDbType.Date).Value = toDate.Value;
        }, cancellationToken);
    }

    /// <summary>
    /// Get sale details by ID
    /// </summary>
    public Task<DataTable> GetSaleDetailsAsync(int saleId, CancellationToken cancellationToken = default)
    {
        return _db.QueryAsync(
            "SELECT * FROM dbo.Order_OrdersDetails WHERE Purchese_ID = @SaleId ORDER BY SN",
            p => p.Add("@SaleId", SqlDbType.Int).Value = saleId, cancellationToken);
    }

    /// <summary>
    /// Get daily cashier summary
    /// </summary>
    public Task<DataTable> GetDailySummaryAsync(int? branchId = null, DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var targetDate = date ?? DateTime.Today;
        
        var sql = @"
SELECT 
    COUNT(*) AS TotalSales,
    ISNULL(SUM(TotalPrices), 0) AS TotalAmount,
    ISNULL(SUM(Tax), 0) AS TotalTax,
    ISNULL(SUM(Net), 0) AS NetTotal,
    ISNULL(SUM(CashMoney), 0) AS CashTotal,
    ISNULL(SUM(CashBank), 0) AS BankTotal
FROM dbo.Order_Orders 
WHERE OrderCashierType = 1";

        if (branchId.HasValue)
            sql += " AND (BranchID = @BranchID OR BranchID IS NULL)";
        
        sql += " AND CONVERT(DATE, Purchases_Date) = CONVERT(DATE, @TargetDate)";

        return _db.QueryAsync(sql, p =>
        {
            if (branchId.HasValue)
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            p.Add("@TargetDate", SqlDbType.Date).Value = targetDate;
        }, cancellationToken);
    }

    #endregion

    #region Receipt Operations

    /// <summary>
    /// Create a cash receipt
    /// </summary>
    public async Task<int> CreateReceiptAsync(
        decimal amount,
        string description,
        int? customerId,
        int? accountId,
        int? paymentType,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO dbo.Account_Receipts (
    Note, UserDate_Add, UserID_Add, UserBranch_Add, UserMacAddress_Add
)
VALUES (
    @Note, GETDATE(), @UserID_Add, @UserBranch_Add, @UserMacAddress_Add
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var result = await _db.QueryAsync(sql, p =>
        {
            p.Add("@Note", SqlDbType.NVarChar, 1000).Value = description.Trim();
            p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Add", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);

        return result.Rows.Count > 0 ? Convert.ToInt32(result.Rows[0][0]) : 0;
    }

    #endregion

    #region Helper Methods

    private static string GetMachineMac()
    {
        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Select(n => n.GetPhysicalAddress()?.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return string.IsNullOrWhiteSpace(mac) ? Environment.MachineName : mac;
        }
        catch
        {
            return Environment.MachineName;
        }
    }

    #endregion
}
