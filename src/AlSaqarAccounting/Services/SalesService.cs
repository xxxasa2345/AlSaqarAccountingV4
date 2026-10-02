using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Operational sales service. Invoice creation goes through the original
/// dbo.Insert_Order_Order_ALL procedure (header + dbo.Items_Orders TVP lines),
/// deletion through dbo.Delete_Order_Orders, listing through
/// dbo.Select_Order_Orders — all from GTSdb2026, no ad-hoc table writes.
/// </summary>
public sealed class SalesService
{
    private readonly DbExecutor _db;
    private readonly AuthorizationService _authorization;

    public SalesService(DbExecutor db)
    {
        _db = db;
        _authorization = new AuthorizationService(db);
    }

    public Task<DataTable> ListAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_Order_Orders", branchId, cancellationToken);

    public Task<DataTable> ListItemsAsync(CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync("dbo.Get_All_Items", cancellationToken: cancellationToken);

    public Task<DataTable> ListCustomersAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchProcedureAsync("dbo.Select_AccountCustomer", branchId, cancellationToken);

    public async Task<SalesEntrySettings> GetEntrySettingsAsync(
        int branchId,
        CancellationToken cancellationToken = default)
    {
        // TblSetting is a settings table, not a branch-keyed lookup table.
        // Read the active/latest settings row instead of treating BranchID as TblSetting.ID.
        const string sql = """
            SELECT TOP (1)
                IsVat,
                PerVat,
                StoreID
            FROM dbo.TblSetting
            ORDER BY ID DESC;
            """;

        using var cn = new SqlConnection(_db.ConnectionString);
        using var cmd = new SqlCommand(sql, cn)
        {
            CommandTimeout = 60
        };

        await cn.OpenAsync(cancellationToken);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return new SalesEntrySettings();

        var enabled = !reader.IsDBNull(0) && reader.GetBoolean(0);
        decimal rate = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
        if (rate > 1m)
            rate /= 100m;

        return new SalesEntrySettings
        {
            VatEnabled = enabled,
            VatRate = enabled ? Math.Max(0m, rate) : 0m,
            DefaultStoreId = reader.IsDBNull(2) ? null : reader.GetInt32(2)
        };
    }

    /// <summary>Inserts a full sales invoice (header + lines) through the
    /// original 61-parameter procedure of GTSdb2026.</summary>
    public async Task CreateAsync(
        SalesInvoice invoice,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (invoice is null)
            throw new ArgumentNullException(nameof(invoice));
        if (!invoice.Lines.Any())
            throw new ArgumentException("لا يمكن حفظ فاتورة بدون أصناف.");
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حفظ الفواتير يتطلب فرعاً فعّالاً.");

        foreach (var line in invoice.Lines)
        {
            if (line.ItemID is null || line.ItemID <= 0)
                throw new ArgumentException("كل سطر في الفاتورة يحتاج إلى صنف صحيح.");
            if ((line.Quantity ?? 0) <= 0)
                throw new ArgumentException("كمية الصنف يجب أن تكون أكبر من صفر.");
            if ((line.UnitPrice ?? 0) < 0)
                throw new ArgumentException("سعر الوحدة لا يمكن أن يكون سالباً.");
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
            _db, "Items_Orders", invoice.Lines.Cast<object>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var contract = await StoredProcedureContract.LoadAsync(
            _db, "dbo.Insert_Order_Order_ALL", cancellationToken).ConfigureAwait(false);

        contract
            .Set("@BranchID", branchId)
            .Set("@Purchases_Date", invoice.InvoiceDate)
            .Set("@Order_Paymant_Type", invoice.PaymentType)
            .Set("@SupplierID", invoice.CustomerId ?? 0)
            .Set("@SupplierName", NullIfEmpty(invoice.CustomerName))
            .Set("@SupplierPhone", NullIfEmpty(invoice.CustomerPhone))
            .Set("@SupplierVatNum", NullIfEmpty(invoice.CustomerVat))
            .Set("@CostCentersID", invoice.CostCenterId)
            .Set("@ProjectID", invoice.ProjectId)
            .Set("@Note", NullIfEmpty(invoice.Note))
            .Set("@NoteNum", NullIfEmpty(invoice.NoteNum))
            .Set("@CostOrder", 0m)
            .Set("@Tax", vat)
            .Set("@AllTax", vat)
            .Set("@TotalPrices", subtotal)
            .Set("@Safy", subtotal - discount)
            .Set("@TotalPrices_Discount", subtotal - discount)
            .Set("@DiscountNum", discount)
            .Set("@DiscountPerantage", 0m)
            .Set("@Tax_Discount", 0m)
            .Set("@TobaccoTax", 0m)
            .Set("@HasmPer", 0m)
            .Set("@HasmAmount", 0m)
            .Set("@Net", net)
            .Set("@CashMoney", invoice.PaymentType == 1 ? amountPaid : 0m)
            .Set("@CashBank", invoice.PaymentType == 2 ? amountPaid : 0m)
            .Set("@AmountPaid", amountPaid)
            .Set("@Rest", net - amountPaid)
            .Set("@AllDiscount", discount)
            .Set("@BounsAmount", 0m)
            .Set("@Charge", 0m)
            .Set("@IsWaiting", false)
            .Set("@OrderCashierType", false)
            .Set("@DateHold", DBNull.Value)
            .Set("@UserID_Add", session.UserId)
            .Set("@UserBranch_Add", branchId)
            .Set("@UserMacAddress_Add", Environment.MachineName)
            .Set("@SalesMan", NullIfEmpty(invoice.SalesMan))
            .Set("@Items", items);

        await _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Insert_Order_Order_ALL", contract.BuildParameters(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Deletes a sales invoice through the original delete procedure.
    /// @PurBranchID is the invoice id and @BranchID the owning branch.</summary>
    public async Task DeleteAsync(
        int invoiceId,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.RequireAsync(
            session, screenId, PermissionAction.Delete, cancellationToken).ConfigureAwait(false);

        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("الحذف يتطلب فرعاً فعّالاً.");

        await _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Delete_Order_Orders",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            },
            cancellationToken).ConfigureAwait(false);
    }

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

/// <summary>Branch-level settings used by the sales entry screen.</summary>
public sealed class SalesEntrySettings
{
    public bool VatEnabled { get; init; }
    public decimal VatRate { get; init; } = 0m;
    public int? DefaultStoreId { get; init; }
}

/// <summary>In-memory sales invoice submitted by the entry screen.</summary>
public sealed class SalesInvoice
{
    public DateTime InvoiceDate { get; set; } = DateTime.Now;

    /// <summary>1 نقدي، 2 بنك، 3 آجل — يطابق Order_Paymant_Type الأصلي.</summary>
    public int PaymentType { get; set; } = 1;

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerVat { get; set; }
    public int? StoreId { get; set; }
    public int? CostCenterId { get; set; }
    public int? ProjectId { get; set; }
    public string? Note { get; set; }
    public string? NoteNum { get; set; }
    public string? SalesMan { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? AmountPaid { get; set; }

    public List<Order_OrdersDetails> Lines { get; } = new();
}

