using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Operational purchases service. Invoice creation goes through the original
/// dbo.Insert_Order_Purchases procedure (header + dbo.Items_Purches TVP in a
/// single call), deletion through dbo.Delete_Order_Purchases, listing through
/// dbo.Select_Order_Purchases — all from GTSdb2026.
/// </summary>
public sealed class PurchasesService
{
    private readonly DbExecutor _db;
    private readonly AuthorizationService _authorization;

    public PurchasesService(DbExecutor db)
    {
        _db = db;
        _authorization = new AuthorizationService(db);
    }

    internal string ConnectionString => _db.ConnectionString;

    public Task<DataTable> ListAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_Order_Purchases", branchId, cancellationToken);

    public Task<DataTable> ListItemsAsync(CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync("dbo.Get_All_Items", cancellationToken: cancellationToken);

    /// <summary>
    /// Loads the original purchase-print dataset through dbo.Print_Order_Purchases.
    /// The catalog proves the contract is exactly @ID int + @BranchID int.
    /// </summary>
    public Task<DataTable> PrintAsync(
        int invoiceId,
        int? branchId,
        CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.Print_Order_Purchases",
            p =>
            {
                p.Add("@ID", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
            },
            cancellationToken);

    public async Task CreateAsync(
        PurchaseInvoice invoice,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (invoice is null)
            throw new ArgumentNullException(nameof(invoice));
        if (!invoice.Lines.Any())
            throw new ArgumentException("لا يمكن حفظ فاتورة مشتريات بدون أصناف.");
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حفظ الفواتير يتطلب فرعاً فعّالاً.");

        foreach (var line in invoice.Lines)
        {
            if (line.ItemID is null || line.ItemID <= 0)
                throw new ArgumentException("كل سطر في الفاتورة يحتاج إلى صنف صحيح.");
            if ((line.Quantity ?? 0) <= 0)
                throw new ArgumentException("كمية الصنف يجب أن تكون أكبر من صفر.");
            if ((line.UnitPrice ?? 0) < 0)
                throw new ArgumentException("سعر الشراء لا يمكن أن يكون سالباً.");
        }

        var branchId = session.BranchId.Value;
        var subtotal = invoice.Lines.Sum(l => l.TotalPrice ?? 0);
        var vat = invoice.Lines.Sum(l => l.VAT ?? 0);
        var discount = ClampToZero(invoice.DiscountAmount);
        if (discount > subtotal)
            throw new ArgumentException("قيمة الخصم أكبر من إجمالي الفاتورة.");
        var net = subtotal - discount + vat;
        var amountPaid = invoice.AmountPaid ?? (invoice.PaymentType == 3 ? 0 : net);
        if (amountPaid < 0 || amountPaid > net)
            throw new ArgumentException("المبلغ المدفوع يجب أن يكون بين صفر وإجمالي الفاتورة.");

        var items = await TvpTableBuilder.CreateAsync(
            _db, "Items_Purches", invoice.Lines.Cast<object>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        await _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Insert_Order_Purchases",
            p =>
            {
                p.Add("@BranchID", SqlDbType.Int).Value = branchId;
                p.Add("@NoteNum", SqlDbType.NVarChar, 200).Value = (object?)NullIfEmpty(invoice.NoteNum) ?? DBNull.Value;
                p.Add("@Tax", SqlDbType.Decimal).Value = vat;
                p.Add("@TotalPrices", SqlDbType.Decimal).Value = subtotal;
                p.Add("@Safy", SqlDbType.Decimal).Value = subtotal - discount;
                p.Add("@DiscountNum", SqlDbType.Decimal).Value = discount;
                p.Add("@DiscountPerantage", SqlDbType.Decimal).Value = 0m;
                p.Add("@Tax_Discount", SqlDbType.Decimal).Value = 0m;
                p.Add("@TotalPrices_Discount", SqlDbType.Decimal).Value = subtotal - discount;
                p.Add("@Net", SqlDbType.Decimal).Value = net;
                p.Add("@Cash", SqlDbType.Decimal).Value = invoice.PaymentType == 1 ? net : 0m;
                p.Add("@SupplierID", SqlDbType.Int).Value = invoice.SupplierId ?? 0;
                p.Add("@Bank", SqlDbType.Decimal).Value = invoice.PaymentType == 2 ? net : 0m;
                p.Add("@Acc_Cash", SqlDbType.Int).Value = (object?)invoice.CashAccountId ?? DBNull.Value;
                p.Add("@Acc_Bank", SqlDbType.Int).Value = (object?)invoice.BankAccountId ?? DBNull.Value;
                p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
                p.Add("@UserBranch_Add", SqlDbType.Int).Value = branchId;
                p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                p.Add("@SalesMan", SqlDbType.NVarChar, 200).Value = (object?)NullIfEmpty(invoice.SalesMan) ?? DBNull.Value;
                p.Add("@Charge", SqlDbType.Decimal).Value = 0m;
                p.Add("@ProjectId", SqlDbType.Int).Value = (object?)invoice.ProjectId ?? DBNull.Value;
                var tvp = p.Add("@Items", SqlDbType.Structured);
                tvp.TypeName = "dbo.Items_Purches";
                tvp.Value = items;
                p.Add("@SupplierName", SqlDbType.NVarChar, 300).Value = (object?)NullIfEmpty(invoice.SupplierName) ?? DBNull.Value;
                p.Add("@SupplierPhone", SqlDbType.NVarChar, 100).Value = (object?)NullIfEmpty(invoice.SupplierPhone) ?? DBNull.Value;
                p.Add("@SupplierVatNum", SqlDbType.NVarChar, 100).Value = (object?)NullIfEmpty(invoice.SupplierVat) ?? DBNull.Value;
                p.Add("@Purchases_Date", SqlDbType.DateTime).Value = invoice.InvoiceDate;
                p.Add("@Order_Paymant_Type", SqlDbType.Int).Value = invoice.PaymentType;
                p.Add("@CostCentersID", SqlDbType.Int).Value = (object?)invoice.CostCenterId ?? DBNull.Value;
                p.Add("@Note", SqlDbType.NVarChar, 400).Value = (object?)NullIfEmpty(invoice.Note) ?? DBNull.Value;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a purchase invoice through the original delete procedure.</summary>
    public Task DeleteAsync(int invoiceId, int? branchId, CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Delete_Order_Purchases",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
            },
            cancellationToken);

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

    private static decimal ClampToZero(decimal value) => value < 0 ? 0 : value;

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>In-memory purchase invoice submitted by the entry screen.</summary>
public sealed class PurchaseInvoice
{
    public DateTime InvoiceDate { get; set; } = DateTime.Now;

    /// <summary>1 نقدي، 2 بنك، 3 آجل — يطابق Order_Paymant_Type الأصلي.</summary>
    public int PaymentType { get; set; } = 1;

    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierPhone { get; set; }
    public string? SupplierVat { get; set; }
    public int? StoreId { get; set; }
    public int? CostCenterId { get; set; }
    public int? ProjectId { get; set; }
    public int? CashAccountId { get; set; }
    public int? BankAccountId { get; set; }
    public string? Note { get; set; }
    public string? NoteNum { get; set; }
    public string? SalesMan { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? AmountPaid { get; set; }

    public List<Order_PurchasesDetails> Lines { get; } = new();
}

