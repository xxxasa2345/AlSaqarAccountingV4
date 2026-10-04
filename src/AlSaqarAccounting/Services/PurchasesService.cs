using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Operational purchase service backed by the original GTS ERP procedures.
/// The database procedure remains responsible for stock and accounting side effects.
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

    public async Task<PurchaseEntrySettings> GetEntrySettingsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP (1) IsVat, PerVat, StoreID
            FROM dbo.TblSetting
            ORDER BY ID DESC;";

        using var cn = new SqlConnection(_db.ConnectionString);
        using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
        await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return new PurchaseEntrySettings();

        var enabled = !reader.IsDBNull(0) && reader.GetBoolean(0);
        var rate = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
        if (rate > 1m)
            rate /= 100m;

        return new PurchaseEntrySettings
        {
            VatEnabled = enabled,
            VatRate = enabled ? Math.Max(0m, rate) : 0m,
            DefaultStoreId = reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2))
        };
    }

    public async Task<(DataTable Header, DataTable Details)> LoadAsync(
        int invoiceId,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("تحميل فاتورة المشتريات يتطلب فرعاً فعّالاً.");

        var header = await _db.QueryAsync(
            @"SELECT TOP (1) *
              FROM dbo.Order_Purchases
              WHERE PurBranchID = @PurBranchID AND BranchID = @BranchID;",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            }, cancellationToken).ConfigureAwait(false);

        if (header.Rows.Count == 0)
            throw new InvalidOperationException("فاتورة المشتريات غير موجودة في الفرع الحالي.");

        var details = await _db.QueryAsync(
            @"SELECT *
              FROM dbo.Order_PurchasesDetails
              WHERE Purchese_ID = @Purchese_ID AND BranchID = @BranchID
              ORDER BY SN;",
            p =>
            {
                p.Add("@Purchese_ID", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            }, cancellationToken).ConfigureAwait(false);

        return (header, details);
    }

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

    /// <summary>
    /// Creates a purchase using dbo.Insert_Order_Purchases.
    /// Cash/Bank are the actual paid amounts, not the invoice total.
    /// The stored procedure then creates the purchase details, inventory movements,
    /// and the corresponding accounting transaction.
    /// </summary>
    public async Task<int> CreateAsync(
        PurchaseInvoice invoice,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        Validate(invoice, session);

        var branchId = session.BranchId!.Value;
        var totals = CalculateTotals(invoice);
        var items = await TvpTableBuilder.CreateAsync(
            _db, "Items_Purches", invoice.Lines.Cast<object>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var contract = await StoredProcedureContract.LoadAsync(
            _db, "dbo.Insert_Order_Purchases", cancellationToken).ConfigureAwait(false);

        ApplyHeader(
            contract,
            invoice,
            branchId,
            session,
            totals,
            items,
            includePurBranchId: false);

        return await _db.ExecuteStoredProcedureReturnValueAsync(
            "dbo.Insert_Order_Purchases",
            contract.BuildParameters(),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a purchase using the original dbo.Update_Order_Purchases procedure.
    /// That procedure deletes/rebuilds the purchase details and recreates the
    /// accounting transaction, so the application must not duplicate those effects.
    /// </summary>
    public async Task<int> UpdateAsync(
        int invoiceId,
        PurchaseInvoice invoice,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        Validate(invoice, session);

        await _authorization.RequireAsync(
            session,
            screenId,
            PermissionAction.Edit,
            cancellationToken).ConfigureAwait(false);

        var branchId = session.BranchId!.Value;
        var exists = await _db.QueryAsync(
            @"SELECT TOP (1) PurBranchID
              FROM dbo.Order_Purchases
              WHERE PurBranchID = @PurBranchID
                AND BranchID = @BranchID;",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = branchId;
            },
            cancellationToken).ConfigureAwait(false);

        if (exists.Rows.Count == 0)
            throw new InvalidOperationException("فاتورة المشتريات غير موجودة في الفرع الحالي.");

        var totals = CalculateTotals(invoice);
        var items = await TvpTableBuilder.CreateAsync(
            _db, "Items_Purches", invoice.Lines.Cast<object>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var contract = await StoredProcedureContract.LoadAsync(
            _db, "dbo.Update_Order_Purchases", cancellationToken).ConfigureAwait(false);

        ApplyHeader(
            contract,
            invoice,
            branchId,
            session,
            totals,
            items,
            includePurBranchId: true);

        contract.Set("@PurBranchID", invoiceId);

        return await _db.ExecuteStoredProcedureReturnValueAsync(
            "dbo.Update_Order_Purchases",
            contract.BuildParameters(),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the complete purchase document using the original ALL procedure.
    /// Delete_Order_Purchases alone removes only the header; the original ERP uses
    /// Delete_Order_Purchases_ALL so details, stock effects, and accounting are
    /// rolled back together.
    /// </summary>
    public async Task DeleteAsync(
        int invoiceId,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حذف فواتير المشتريات يتطلب فرعاً فعّالاً.");

        await _authorization.RequireAsync(
            session,
            screenId,
            PermissionAction.Delete,
            cancellationToken).ConfigureAwait(false);

        await _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Delete_Order_Purchases_ALL",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId.Value;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(PurchaseInvoice invoice, AppSession session)
    {
        if (invoice is null)
            throw new ArgumentNullException(nameof(invoice));
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حفظ فواتير المشتريات يتطلب فرعاً فعّالاً.");
        if (invoice.Lines.Count == 0)
            throw new ArgumentException("لا يمكن حفظ فاتورة مشتريات بدون أصناف.");

        foreach (var line in invoice.Lines)
        {
            if (!line.ItemID.HasValue || line.ItemID.Value <= 0)
                throw new ArgumentException("كل سطر في الفاتورة يحتاج إلى صنف صحيح.");
            if (!line.Quantity.HasValue || line.Quantity.Value <= 0)
                throw new ArgumentException("كمية الصنف يجب أن تكون أكبر من صفر.");
            if ((line.UnitPrice ?? 0m) < 0m)
                throw new ArgumentException("سعر الشراء لا يمكن أن يكون سالباً.");
        }
    }

    private static PurchaseTotals CalculateTotals(PurchaseInvoice invoice)
    {
        var subtotal = decimal.Round(
            invoice.Lines.Sum(l => l.TotalPrice ?? 0m), 2);

        var vat = decimal.Round(
            invoice.Lines.Sum(l => l.VAT ?? 0m), 2);

        var discount = Math.Max(0m, invoice.DiscountAmount);
        if (discount > subtotal)
            throw new ArgumentException("قيمة الخصم أكبر من إجمالي الفاتورة.");

        var safy = decimal.Round(subtotal - discount, 2);
        var net = decimal.Round(safy + vat, 2);

        var paid = invoice.AmountPaid ?? (invoice.PaymentType == 3 ? 0m : net);
        paid = decimal.Round(paid, 2);

        if (paid < 0m || paid > net)
            throw new ArgumentException("المبلغ المدفوع يجب أن يكون بين صفر وإجمالي الفاتورة.");

        var cash = invoice.PaymentType == 1 ? paid : 0m;
        var bank = invoice.PaymentType == 2 ? paid : 0m;

        return new PurchaseTotals(subtotal, vat, discount, safy, net, paid, cash, bank);
    }

    private static void ApplyHeader(
        StoredProcedureContract contract,
        PurchaseInvoice invoice,
        int branchId,
        AppSession session,
        PurchaseTotals totals,
        DataTable items,
        bool includePurBranchId)
    {
        if (includePurBranchId)
            contract.Set("@PurBranchID", 0); // replaced by caller immediately

        contract
            .Set("@BranchID", branchId)
            .Set("@SupplierID", invoice.SupplierId ?? 0)
            .Set("@SupplierName", NullIfEmpty(invoice.SupplierName))
            .Set("@SupplierPhone", NullIfEmpty(invoice.SupplierPhone))
            .Set("@SupplierVatNum", NullIfEmpty(invoice.SupplierVat))
            .Set("@Purchases_Date", invoice.InvoiceDate)
            .Set("@Order_Paymant_Type", invoice.PaymentType)
            .Set("@CostCentersID", invoice.CostCenterId ?? 0)
            .Set("@Note", NullIfEmpty(invoice.Note))
            .Set("@NoteNum", NullIfEmpty(invoice.NoteNum))
            .Set("@Tax", totals.Vat)
            .Set("@TotalPrices", totals.Subtotal)
            .Set("@Safy", totals.Safy)
            .Set("@DiscountNum", totals.Discount)
            .Set("@DiscountPerantage", 0m)
            .Set("@Tax_Discount", 0m)
            .Set("@TotalPrices_Discount", totals.Safy)
            .Set("@Net", totals.Net)
            .Set("@Cash", totals.Cash)
            .Set("@Bank", totals.Bank)
            .Set("@Acc_Cash", invoice.CashAccountId)
            .Set("@Acc_Bank", invoice.BankAccountId)
            .Set("@UserID_Add", session.UserId)
            .Set("@UserBranch_Add", branchId)
            .Set("@UserMacAddress_Add", GetMachineAddress())
            .Set("@SalesMan", NullIfEmpty(invoice.SalesMan) ?? session.UserName)
            .Set("@Charge", 0m)
            .Set("@ProjectId", invoice.ProjectId)
            .Set("@Items", items);
    }

    private Task<DataTable> ExecuteBranchProcedureAsync(
        string procedureName,
        int? branchId,
        CancellationToken cancellationToken)
        => _db.ExecuteStoredProcedureAsync(
            procedureName,
            p =>
            {
                if (!branchId.HasValue)
                    throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            },
            cancellationToken);

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static string GetMachineAddress()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                .Select(n => n.GetPhysicalAddress()?.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? Environment.MachineName;
        }
        catch
        {
            return Environment.MachineName;
        }
    }

    private readonly record struct PurchaseTotals(
        decimal Subtotal,
        decimal Vat,
        decimal Discount,
        decimal Safy,
        decimal Net,
        decimal Paid,
        decimal Cash,
        decimal Bank);
}

/// <summary>In-memory purchase invoice submitted by an entry screen.</summary>
public sealed class PurchaseEntrySettings
{
    public bool VatEnabled { get; init; }
    public decimal VatRate { get; init; }
    public int? DefaultStoreId { get; init; }
}

public sealed class PurchaseInvoice
{
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
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
