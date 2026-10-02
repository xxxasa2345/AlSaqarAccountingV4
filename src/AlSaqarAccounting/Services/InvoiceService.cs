using System.Data;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Invoice management.
/// Handles sales invoices, purchase invoices, and invoice processing.
/// </summary>
public sealed class InvoiceService
{
    private readonly DbExecutor _db;
    private readonly AuthorizationService _authorization;

    public InvoiceService(DbExecutor db)
    {
        _db = db;
        _authorization = new AuthorizationService(db);
    }

    #region Sales Invoices

    /// <summary>
    /// Create a new sales invoice
    /// </summary>
    public async Task<int> CreateSalesInvoiceAsync(
        Order_Orders invoice,
        List<Order_OrdersDetails> details,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        // Validate
        if (details == null || details.Count == 0)
            throw new ArgumentException("Invoice must have at least one item");
        
        if (string.IsNullOrWhiteSpace(invoice.SupplierName))
            throw new ArgumentException("Customer name is required");

        // Start transaction
        using var cn = new SqlConnection(_db.ConnectionString);
        await cn.OpenAsync(cancellationToken);
        
        using var transaction = cn.BeginTransaction();
        
        try
        {
            // Insert invoice header
            invoice.OrderCashierType = false;
            invoice.BranchID = session.BranchId;
            invoice.UserID_Add = session.UserId;
            invoice.UserBranch_Add = session.BranchId;
            invoice.UserMacAddress_Add = GetMachineMac();
            invoice.UserDate_Add = DateTime.Now;
            invoice.Purchases_Date = DateTime.Now;

            const string invoiceSql = @"
INSERT INTO dbo.Order_Orders (
    PurBranchID, BranchID, SupplierID, SupplierName, SupplierPhone, SupplierVatNum,
    Purchases_Date, Note, NoteNum, CostOrder, Tax, TotalPrices, Safy,
    DiscountNum, DiscountPerantage, Tax_Discount, TotalPrices_Discount, AllTax, Net,
    CashMoney, CashBank, OrderCashierType, RoomNum, TableNum,
    UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add,
    BuildingNum, Street, District, City, Country, PostalCode, AdditionalNum, CommercialRecord,
    OrderTypeElectronicInvoiceId, NormalOrSimpleInvoice, ThirdParty, NominalInvoice,
    ExportInvoice, SummaryInvoice, SelfInvoice, AmountPaid, Rest, CarName, CarModel,
    PlateNumber, ChassisNum, CarColor, Counter
)
VALUES (
    @PurBranchID, @BranchID, @SupplierID, @SupplierName, @SupplierPhone, @SupplierVatNum,
    @Purchases_Date, @Note, @NoteNum, @CostOrder, @Tax, @TotalPrices, @Safy,
    @DiscountNum, @DiscountPerantage, @Tax_Discount, @TotalPrices_Discount, @AllTax, @Net,
    @CashMoney, @CashBank, @OrderCashierType, @RoomNum, @TableNum,
    @UserID_Add, @UserBranch_Add, @UserMacAddress_Add, @UserDate_Add,
    @BuildingNum, @Street, @District, @City, @Country, @PostalCode, @AdditionalNum, @CommercialRecord,
    @OrderTypeElectronicInvoiceId, @NormalOrSimpleInvoice, @ThirdParty, @NominalInvoice,
    @ExportInvoice, @SummaryInvoice, @SelfInvoice, @AmountPaid, @Rest, @CarName, @CarModel,
    @PlateNumber, @ChassisNum, @CarColor, @Counter
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var invoiceCmd = new SqlCommand(invoiceSql, cn, transaction);
            invoiceCmd.Parameters.Add("@PurBranchID", SqlDbType.Int).Value = (object)invoice.PurBranchID ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = (object)invoice.BranchID ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SupplierID", SqlDbType.Int).Value = (object)invoice.SupplierID ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 300).Value = invoice.SupplierName.Trim();
            invoiceCmd.Parameters.Add("@SupplierPhone", SqlDbType.NVarChar, 100).Value = (object)invoice.SupplierPhone ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SupplierVatNum", SqlDbType.NVarChar, 100).Value = (object)invoice.SupplierVatNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Purchases_Date", SqlDbType.DateTime).Value = invoice.Purchases_Date;
            invoiceCmd.Parameters.Add("@Note", SqlDbType.NVarChar, 1000).Value = (object)invoice.Note ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@NoteNum", SqlDbType.NVarChar, 100).Value = (object)invoice.NoteNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@CostOrder", SqlDbType.Decimal).Value = (object)invoice.CostOrder ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Tax", SqlDbType.Decimal).Value = (object)invoice.Tax ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@TotalPrices", SqlDbType.Decimal).Value = (object)invoice.TotalPrices ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Safy", SqlDbType.Decimal).Value = (object)invoice.Safy ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@DiscountNum", SqlDbType.Decimal).Value = (object)invoice.DiscountNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@DiscountPerantage", SqlDbType.Decimal).Value = (object)invoice.DiscountPerantage ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Tax_Discount", SqlDbType.Decimal).Value = (object)invoice.Tax_Discount ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@TotalPrices_Discount", SqlDbType.Decimal).Value = (object)invoice.TotalPrices_Discount ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@AllTax", SqlDbType.Decimal).Value = (object)invoice.AllTax ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Net", SqlDbType.Decimal).Value = (object)invoice.Net ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@CashMoney", SqlDbType.Decimal).Value = (object)invoice.CashMoney ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@CashBank", SqlDbType.Decimal).Value = (object)invoice.CashBank ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@OrderCashierType", SqlDbType.Bit).Value = invoice.OrderCashierType;
            invoiceCmd.Parameters.Add("@RoomNum", SqlDbType.Int).Value = (object)invoice.RoomNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@TableNum", SqlDbType.Int).Value = (object)invoice.TableNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@UserID_Add", SqlDbType.Int).Value = invoice.UserID_Add;
            invoiceCmd.Parameters.Add("@UserBranch_Add", SqlDbType.Int).Value = (object)invoice.UserBranch_Add ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = invoice.UserMacAddress_Add;
            invoiceCmd.Parameters.Add("@UserDate_Add", SqlDbType.DateTime).Value = invoice.UserDate_Add;
            invoiceCmd.Parameters.Add("@BuildingNum", SqlDbType.NVarChar, 100).Value = (object)invoice.BuildingNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Street", SqlDbType.NVarChar, 200).Value = (object)invoice.Street ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@District", SqlDbType.NVarChar, 200).Value = (object)invoice.District ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = (object)invoice.City ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Country", SqlDbType.NVarChar, 100).Value = (object)invoice.Country ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@PostalCode", SqlDbType.NVarChar, 50).Value = (object)invoice.PostalCode ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@AdditionalNum", SqlDbType.NVarChar, 100).Value = (object)invoice.AdditionalNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@CommercialRecord", SqlDbType.NVarChar, 100).Value = (object)invoice.CommercialRecord ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@OrderTypeElectronicInvoiceId", SqlDbType.NVarChar, 100).Value = (object)invoice.OrderTypeElectronicInvoiceId ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@NormalOrSimpleInvoice", SqlDbType.NVarChar, 50).Value = (object)invoice.NormalOrSimpleInvoice ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@ThirdParty", SqlDbType.Bit).Value = (object)invoice.ThirdParty ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@NominalInvoice", SqlDbType.Bit).Value = (object)invoice.NominalInvoice ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@ExportInvoice", SqlDbType.Bit).Value = (object)invoice.ExportInvoice ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SummaryInvoice", SqlDbType.Bit).Value = (object)invoice.SummaryInvoice ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SelfInvoice", SqlDbType.Bit).Value = (object)invoice.SelfInvoice ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@AmountPaid", SqlDbType.Decimal).Value = (object)invoice.AmountPaid ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Rest", SqlDbType.Decimal).Value = (object)invoice.Rest ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@CarName", SqlDbType.NVarChar, 100).Value = (object)invoice.CarName ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@CarModel", SqlDbType.NVarChar, 100).Value = (object)invoice.CarModel ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@PlateNumber", SqlDbType.NVarChar, 50).Value = (object)invoice.PlateNumber ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@ChassisNum", SqlDbType.NVarChar, 100).Value = (object)invoice.ChassisNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@CarColor", SqlDbType.NVarChar, 50).Value = (object)invoice.CarColor ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Counter", SqlDbType.NVarChar, 50).Value = (object)invoice.Counter ?? DBNull.Value;

            var invoiceId = Convert.ToInt32(await invoiceCmd.ExecuteScalarAsync(cancellationToken));

            // Insert invoice details
            foreach (var detail in details)
            {
                detail.Purchese_ID = invoiceId;
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
            return invoiceId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Get list of invoices
    /// </summary>
    public Task<DataTable> GetInvoicesAsync(bool salesInvoices = true, int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var sql = @"SELECT * FROM dbo.Order_Orders 
                   WHERE OrderCashierType = 0";

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
    /// Get invoice details by ID
    /// </summary>
    public Task<DataTable> GetInvoiceDetailsAsync(
        int invoiceId,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("عرض تفاصيل الفاتورة يتطلب فرعاً فعّالاً.");

        return _db.QueryAsync(
            @"SELECT d.*
              FROM dbo.Order_OrdersDetails AS d
              INNER JOIN dbo.Order_Orders AS h ON h.ID = d.Purchese_ID
              WHERE d.Purchese_ID = @InvoiceId
                AND (h.BranchID = @BranchID OR h.BranchID IS NULL)
              ORDER BY d.SN;",
            p =>
            {
                p.Add("@InvoiceId", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            },
            cancellationToken);
    }

    /// <summary>
    /// Get invoice by ID
    /// </summary>
    public Task<Order_Orders?> GetInvoiceByIdAsync(
        int invoiceId,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("عرض الفاتورة يتطلب فرعاً فعّالاً.");

        return _db.QuerySingleAsync<Order_Orders>(
            @"SELECT *
              FROM dbo.Order_Orders
              WHERE ID = @InvoiceId
                AND (BranchID = @BranchID OR BranchID IS NULL);",
            p =>
            {
                p.Add("@InvoiceId", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            },
            cancellationToken);
    }

    /// <summary>
    /// Update invoice
    /// </summary>
    public async Task<int> UpdateInvoiceAsync(
        Order_Orders invoice,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("تعديل الفواتير يتطلب فرعاً فعّالاً.");

        await _authorization.RequireAsync(
            session,
            screenId,
            PermissionAction.Edit,
            cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE dbo.Order_Orders
SET 
    SupplierName = @SupplierName,
    SupplierPhone = @SupplierPhone,
    SupplierVatNum = @SupplierVatNum,
    Note = @Note,
    NoteNum = @NoteNum,
    CostOrder = @CostOrder,
    Tax = @Tax,
    TotalPrices = @TotalPrices,
    Safy = @Safy,
    DiscountNum = @DiscountNum,
    DiscountPerantage = @DiscountPerantage,
    Tax_Discount = @Tax_Discount,
    TotalPrices_Discount = @TotalPrices_Discount,
    AllTax = @AllTax,
    Net = @Net,
    CashMoney = @CashMoney,
    CashBank = @CashBank,
    UserID_Update = @UserID_Update,
    UserBranch_Update = @UserBranch_Update,
    UserMacAddress_Update = @UserMacAddress_Update,
    UserDate_Update = GETDATE()
WHERE ID = @ID AND (UserBranch_Add = @BranchID);";

        return await _db.ExecuteAsync(sql, p =>
        {
            p.Add("@ID", SqlDbType.Int).Value = invoice.ID;
            p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            p.Add("@SupplierName", SqlDbType.NVarChar, 300).Value = invoice.SupplierName.Trim();
            p.Add("@SupplierPhone", SqlDbType.NVarChar, 100).Value = (object)invoice.SupplierPhone ?? DBNull.Value;
            p.Add("@SupplierVatNum", SqlDbType.NVarChar, 100).Value = (object)invoice.SupplierVatNum ?? DBNull.Value;
            p.Add("@Note", SqlDbType.NVarChar, 1000).Value = (object)invoice.Note ?? DBNull.Value;
            p.Add("@NoteNum", SqlDbType.NVarChar, 100).Value = (object)invoice.NoteNum ?? DBNull.Value;
            p.Add("@CostOrder", SqlDbType.Decimal).Value = (object)invoice.CostOrder ?? DBNull.Value;
            p.Add("@Tax", SqlDbType.Decimal).Value = (object)invoice.Tax ?? DBNull.Value;
            p.Add("@TotalPrices", SqlDbType.Decimal).Value = (object)invoice.TotalPrices ?? DBNull.Value;
            p.Add("@Safy", SqlDbType.Decimal).Value = (object)invoice.Safy ?? DBNull.Value;
            p.Add("@DiscountNum", SqlDbType.Decimal).Value = (object)invoice.DiscountNum ?? DBNull.Value;
            p.Add("@DiscountPerantage", SqlDbType.Decimal).Value = (object)invoice.DiscountPerantage ?? DBNull.Value;
            p.Add("@Tax_Discount", SqlDbType.Decimal).Value = (object)invoice.Tax_Discount ?? DBNull.Value;
            p.Add("@TotalPrices_Discount", SqlDbType.Decimal).Value = (object)invoice.TotalPrices_Discount ?? DBNull.Value;
            p.Add("@AllTax", SqlDbType.Decimal).Value = (object)invoice.AllTax ?? DBNull.Value;
            p.Add("@Net", SqlDbType.Decimal).Value = (object)invoice.Net ?? DBNull.Value;
            p.Add("@CashMoney", SqlDbType.Decimal).Value = (object)invoice.CashMoney ?? DBNull.Value;
            p.Add("@CashBank", SqlDbType.Decimal).Value = (object)invoice.CashBank ?? DBNull.Value;
            p.Add("@UserID_Update", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Update", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Update", SqlDbType.NVarChar, 200).Value = GetMachineMac();
        }, cancellationToken);
    }

    /// <summary>
    /// Delete invoice
    /// </summary>
    public async Task<int> DeleteInvoiceAsync(
        int invoiceId,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حذف الفواتير يتطلب فرعاً فعّالاً.");

        await _authorization.RequireAsync(
            session,
            screenId,
            PermissionAction.Delete,
            cancellationToken).ConfigureAwait(false);

        // Delete details first, restricted to the active branch.
        await _db.ExecuteAsync(
            @"DELETE d
              FROM dbo.Order_OrdersDetails AS d
              WHERE d.Purchese_ID = @InvoiceId
                AND EXISTS (
                    SELECT 1
                    FROM dbo.Order_Orders AS h
                    WHERE h.ID = @InvoiceId
                      AND (h.BranchID = @BranchID OR h.BranchID IS NULL)
                );",
            p =>
            {
                p.Add("@InvoiceId", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            },
            cancellationToken).ConfigureAwait(false);

        // Delete header, restricted to sales invoices owned by the active branch.
        return await _db.ExecuteAsync(
            @"DELETE FROM dbo.Order_Orders
              WHERE ID = @InvoiceId
                AND OrderCashierType = 0
                AND (BranchID = @BranchID OR BranchID IS NULL);",
            p =>
            {
                p.Add("@InvoiceId", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            },
            cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Purchase Invoices

    /// <summary>
    /// Create a new purchase invoice
    /// </summary>
    public async Task<int> CreatePurchaseInvoiceAsync(
        Order_Purchases invoice,
        List<Order_PurchasesDetails> details,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        // Validate
        if (details == null || details.Count == 0)
            throw new ArgumentException("Invoice must have at least one item");
        
        if (string.IsNullOrWhiteSpace(invoice.SupplierName))
            throw new ArgumentException("Supplier name is required");

        // Start transaction
        using var cn = new SqlConnection(_db.ConnectionString);
        await cn.OpenAsync(cancellationToken);
        
        using var transaction = cn.BeginTransaction();
        
        try
        {
            // Insert invoice header
            invoice.BranchID = session.BranchId;
            invoice.UserID_Add = session.UserId;
            invoice.UserBranch_Add = session.BranchId;
            invoice.UserMacAddress_Add = GetMachineMac();
            invoice.UserDate_Add = DateTime.Now;
            invoice.Purchases_Date = DateTime.Now;

            const string invoiceSql = @"
INSERT INTO dbo.Order_Purchases (
    PurBranchID, BranchID, SupplierID, SupplierName, SupplierPhone, SupplierVatNum,
    Purchases_Date, Note, NoteNum, Tax, TotalPrices, Safy,
    DiscountNum, DiscountPerantage, Tax_Discount, TotalPrices_Discount, Net,
    UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add,
    Cash, Bank, CostCentersID, Order_Paymant_Type, ProjectId, YearId,
    Acc_Cash, Acc_Bank, SalesMan, Charge
)
VALUES (
    @PurBranchID, @BranchID, @SupplierID, @SupplierName, @SupplierPhone, @SupplierVatNum,
    @Purchases_Date, @Note, @NoteNum, @Tax, @TotalPrices, @Safy,
    @DiscountNum, @DiscountPerantage, @Tax_Discount, @TotalPrices_Discount, @Net,
    @UserID_Add, @UserBranch_Add, @UserMacAddress_Add, @UserDate_Add,
    @Cash, @Bank, @CostCentersID, @Order_Paymant_Type, @ProjectId, @YearId,
    @Acc_Cash, @Acc_Bank, @SalesMan, @Charge
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var invoiceCmd = new SqlCommand(invoiceSql, cn, transaction);
            invoiceCmd.Parameters.Add("@PurBranchID", SqlDbType.Int).Value = (object)invoice.PurBranchID ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = (object)invoice.BranchID ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SupplierID", SqlDbType.Int).Value = (object)invoice.SupplierID ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 300).Value = invoice.SupplierName.Trim();
            invoiceCmd.Parameters.Add("@SupplierPhone", SqlDbType.NVarChar, 100).Value = (object)invoice.SupplierPhone ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SupplierVatNum", SqlDbType.NVarChar, 100).Value = (object)invoice.SupplierVatNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Purchases_Date", SqlDbType.DateTime).Value = invoice.Purchases_Date;
            invoiceCmd.Parameters.Add("@Note", SqlDbType.NVarChar, 1000).Value = (object)invoice.Note ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@NoteNum", SqlDbType.NVarChar, 100).Value = (object)invoice.NoteNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Tax", SqlDbType.Decimal).Value = (object)invoice.Tax ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@TotalPrices", SqlDbType.Decimal).Value = (object)invoice.TotalPrices ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Safy", SqlDbType.Decimal).Value = (object)invoice.Safy ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@DiscountNum", SqlDbType.Decimal).Value = (object)invoice.DiscountNum ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@DiscountPerantage", SqlDbType.Decimal).Value = (object)invoice.DiscountPerantage ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Tax_Discount", SqlDbType.Decimal).Value = (object)invoice.Tax_Discount ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@TotalPrices_Discount", SqlDbType.Decimal).Value = (object)invoice.TotalPrices_Discount ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Net", SqlDbType.Decimal).Value = (object)invoice.Net ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@UserID_Add", SqlDbType.Int).Value = invoice.UserID_Add;
            invoiceCmd.Parameters.Add("@UserBranch_Add", SqlDbType.Int).Value = (object)invoice.UserBranch_Add ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = invoice.UserMacAddress_Add;
            invoiceCmd.Parameters.Add("@UserDate_Add", SqlDbType.DateTime).Value = invoice.UserDate_Add;
            invoiceCmd.Parameters.Add("@Cash", SqlDbType.Decimal).Value = (object)invoice.Cash ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Bank", SqlDbType.Decimal).Value = (object)invoice.Bank ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@CostCentersID", SqlDbType.Int).Value = (object)invoice.CostCentersID ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Order_Paymant_Type", SqlDbType.Int).Value = (object)invoice.Order_Paymant_Type ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@ProjectId", SqlDbType.Int).Value = (object)invoice.ProjectId ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@YearId", SqlDbType.Int).Value = (object)invoice.YearId ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Acc_Cash", SqlDbType.Int).Value = (object)invoice.Acc_Cash ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Acc_Bank", SqlDbType.Int).Value = (object)invoice.Acc_Bank ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@SalesMan", SqlDbType.NVarChar, 100).Value = (object)invoice.SalesMan ?? DBNull.Value;
            invoiceCmd.Parameters.Add("@Charge", SqlDbType.Decimal).Value = (object)invoice.Charge ?? DBNull.Value;

            var invoiceId = Convert.ToInt32(await invoiceCmd.ExecuteScalarAsync(cancellationToken));

            // Insert invoice details
            foreach (var detail in details)
            {
                detail.Purchese_ID = invoiceId;
                detail.BranchID = session.BranchId;

                const string detailSql = @"
INSERT INTO dbo.Order_PurchasesDetails (
    Purchese_ID, SN, ItemID, BranchID, StoreID, ItemUnitID, Quantity,
    UnitPrice, TotalPrice, VAT, NetUnitPrice, NetTotalPrice, VAT_Discount,
    ItemUnitType, Bounce, NoteItem, UnitNumber, SellPrice, DiscNum, DiscPercent
)
VALUES (
    @Purchese_ID, @SN, @ItemID, @BranchID, @StoreID, @ItemUnitID, @Quantity,
    @UnitPrice, @TotalPrice, @VAT, @NetUnitPrice, @NetTotalPrice, @VAT_Discount,
    @ItemUnitType, @Bounce, @NoteItem, @UnitNumber, @SellPrice, @DiscNum, @DiscPercent
);";

                using var detailCmd = new SqlCommand(detailSql, cn, transaction);
                detailCmd.Parameters.Add("@Purchese_ID", SqlDbType.Int).Value = detail.Purchese_ID;
                detailCmd.Parameters.Add("@SN", SqlDbType.Int).Value = detail.SN;
                detailCmd.Parameters.Add("@ItemID", SqlDbType.Int).Value = (object)detail.ItemID ?? DBNull.Value;
                detailCmd.Parameters.Add("@BranchID", SqlDbType.Int).Value = (object)detail.BranchID ?? DBNull.Value;
                detailCmd.Parameters.Add("@StoreID", SqlDbType.Int).Value = (object)detail.StoreID ?? DBNull.Value;
                detailCmd.Parameters.Add("@ItemUnitID", SqlDbType.Int).Value = (object)detail.ItemUnitID ?? DBNull.Value;
                detailCmd.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = (object)detail.Quantity ?? DBNull.Value;
                detailCmd.Parameters.Add("@UnitPrice", SqlDbType.Decimal).Value = (object)detail.UnitPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@TotalPrice", SqlDbType.Decimal).Value = (object)detail.TotalPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@VAT", SqlDbType.Decimal).Value = (object)detail.VAT ?? DBNull.Value;
                detailCmd.Parameters.Add("@NetUnitPrice", SqlDbType.Decimal).Value = (object)detail.NetUnitPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@NetTotalPrice", SqlDbType.Decimal).Value = (object)detail.NetTotalPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@VAT_Discount", SqlDbType.Decimal).Value = (object)detail.VAT_Discount ?? DBNull.Value;
                detailCmd.Parameters.Add("@ItemUnitType", SqlDbType.NVarChar, 100).Value = (object)detail.ItemUnitType ?? DBNull.Value;
                detailCmd.Parameters.Add("@Bounce", SqlDbType.Decimal).Value = (object)detail.Bounce ?? DBNull.Value;
                detailCmd.Parameters.Add("@NoteItem", SqlDbType.NVarChar, 500).Value = (object)detail.NoteItem ?? DBNull.Value;
                detailCmd.Parameters.Add("@UnitNumber", SqlDbType.Decimal).Value = (object)detail.UnitNumber ?? DBNull.Value;
                detailCmd.Parameters.Add("@SellPrice", SqlDbType.Decimal).Value = (object)detail.SellPrice ?? DBNull.Value;
                detailCmd.Parameters.Add("@DiscNum", SqlDbType.Decimal).Value = (object)detail.DiscNum ?? DBNull.Value;
                detailCmd.Parameters.Add("@DiscPercent", SqlDbType.Decimal).Value = (object)detail.DiscPercent ?? DBNull.Value;

                await detailCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
            return invoiceId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Get list of purchase invoices
    /// </summary>
    public Task<DataTable> GetPurchaseInvoicesAsync(int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var sql = @"SELECT * FROM dbo.Order_Purchases";

        if (branchId.HasValue)
            sql += " WHERE BranchID = @BranchID";
        
        if (fromDate.HasValue)
            sql += (branchId.HasValue ? " AND" : " WHERE") + " Purchases_Date >= @FromDate";
        
        if (toDate.HasValue)
            sql += (branchId.HasValue || fromDate.HasValue ? " AND" : " WHERE") + " Purchases_Date <= @ToDate";
        
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
    /// Get purchase invoice details from the forensic Order_PurchasesDetails table.
    /// </summary>
    public Task<DataTable> GetPurchaseInvoiceDetailsAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        return _db.QueryAsync(
            "SELECT * FROM dbo.Order_PurchasesDetails WHERE Purchese_ID = @InvoiceId ORDER BY SN",
            p => p.Add("@InvoiceId", SqlDbType.Int).Value = invoiceId,
            cancellationToken);
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
