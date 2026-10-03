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
        return await new OriginalSalesFlowService(_db)
            .CreateAsync(invoice, details, session, cancellationToken)
            .ConfigureAwait(false);
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
        return await new OriginalSalesFlowService(_db)
            .UpdateAsync(invoice, session, screenId, cancellationToken)
            .ConfigureAwait(false);
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
        await new OriginalSalesFlowService(_db)
            .DeleteAsync(invoiceId, session, screenId, cancellationToken)
            .ConfigureAwait(false);

        return 1;
    }

    #endregion

    #region Purchase Invoices    #endregion

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
