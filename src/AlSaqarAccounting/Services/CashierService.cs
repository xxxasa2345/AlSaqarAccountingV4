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
        if (order is null) throw new ArgumentNullException(nameof(order));
        if (details is null || details.Count == 0)
            throw new ArgumentException("يجب أن تحتوي الفاتورة على صنف واحد على الأقل.");
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حفظ فاتورة الكاشير يتطلب فرعاً فعّالاً.");
        if (string.IsNullOrWhiteSpace(order.SupplierName))
            throw new ArgumentException("اسم العميل مطلوب.");

        foreach (var detail in details)
        {
            if (!detail.ItemID.HasValue || detail.ItemID <= 0)
                throw new ArgumentException("كل سطر يجب أن يحتوي على صنف صحيح.");
            if (!detail.Quantity.HasValue || detail.Quantity <= 0)
                throw new ArgumentException("كمية الصنف يجب أن تكون أكبر من صفر.");
            detail.BranchID = session.BranchId;
        }

        order.OrderCashierType = true;
        order.BranchID = session.BranchId;
        order.UserID_Add = session.UserId;
        order.UserBranch_Add = session.BranchId;
        order.UserMacAddress_Add = GetMachineMac();
        order.UserDate_Add = DateTime.Now;
        order.Purchases_Date = order.Purchases_Date == default ? DateTime.Now : order.Purchases_Date;

        var invoice = new SalesInvoice
        {
            InvoiceDate = order.Purchases_Date,
            PaymentType = order.Order_Paymant_Type.GetValueOrDefault(order.CashMoney.GetValueOrDefault() > 0m ? 1 : 2),
            CustomerId = order.SupplierID,
            CustomerName = order.SupplierName,
            CustomerPhone = order.SupplierPhone,
            CustomerVat = order.SupplierVatNum,
            CostCenterId = null,
            ProjectId = null,
            Note = order.Note,
            NoteNum = order.NoteNum,
            SalesMan = null,
            DiscountAmount = Math.Max(0m, order.DiscountNum ?? 0m),
            AmountPaid = Math.Max(0m, order.CashMoney.GetValueOrDefault() + order.CashBank.GetValueOrDefault())
        };

        foreach (var detail in details)
            invoice.Lines.Add(detail);

        var sales = new SalesService(_db);
        return await sales.CreateAsync(invoice, session, cancellationToken).ConfigureAwait(false);
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
