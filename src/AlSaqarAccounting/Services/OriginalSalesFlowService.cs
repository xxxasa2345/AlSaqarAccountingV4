using System.Data;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Exact sales-document gateway for the original GTS ERP procedures.
/// It intentionally delegates stock and accounting to the database procedures
/// instead of duplicating their side effects in application code.
/// </summary>
public sealed class OriginalSalesFlowService
{
    private readonly DbExecutor _db;
    private readonly AuthorizationService _authorization;

    public OriginalSalesFlowService(DbExecutor db)
    {
        _db = db;
        _authorization = new AuthorizationService(db);
    }

    public async Task<int> CreateAsync(
        Order_Orders invoice,
        IReadOnlyList<Order_OrdersDetails> details,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        Validate(session, details);

        var branchId = session.BranchId!.Value;
        foreach (var detail in details)
        {
            detail.BranchID = branchId;
            detail.IsWaiting = invoice.IsWaiting;
        }

        var items = await TvpTableBuilder.CreateAsync(
            _db, "Items_Orders", details.Cast<object>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var contract = await StoredProcedureContract.LoadAsync(
            _db, "dbo.Insert_Order_Order_ALL", cancellationToken).ConfigureAwait(false);

        ApplyHeader(contract, invoice, branchId, session, items);

        return await _db.ExecuteStoredProcedureReturnValueAsync(
            "dbo.Insert_Order_Order_ALL",
            contract.BuildParameters(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(
        Order_Orders incoming,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("تعديل الفواتير يتطلب فرعاً فعّالاً.");

        await _authorization.RequireAsync(
            session, screenId, PermissionAction.Edit, cancellationToken)
            .ConfigureAwait(false);

        var branchId = session.BranchId.Value;
        var existing = await _db.QuerySingleAsync<Order_Orders>(
            @"SELECT TOP (1) *
              FROM dbo.Order_Orders
              WHERE (ID = @ID OR PurBranchID = @ID)
                AND BranchID = @BranchID;",
            p =>
            {
                p.Add("@ID", System.Data.SqlClient.SqlDbType.Int).Value = incoming.ID;
                p.Add("@BranchID", System.Data.SqlClient.SqlDbType.Int).Value = branchId;
            },
            cancellationToken).ConfigureAwait(false);

        if (existing is null)
            throw new InvalidOperationException("الفاتورة المطلوب تعديلها غير موجودة في الفرع الحالي.");

        Merge(existing, incoming);
        var purBranchId = existing.PurBranchID ?? existing.ID;

        var table = await _db.QueryAsync(
            @"SELECT d.*
              FROM dbo.Order_OrdersDetails AS d
              WHERE d.Purchese_ID = @PurBranchID
                AND d.BranchID = @BranchID
              ORDER BY d.SN;",
            p =>
            {
                p.Add("@PurBranchID", System.Data.SqlClient.SqlDbType.Int).Value = purBranchId;
                p.Add("@BranchID", System.Data.SqlClient.SqlDbType.Int).Value = branchId;
            },
            cancellationToken).ConfigureAwait(false);

        var details = MapDetails(table, branchId);
        if (details.Count == 0)
            throw new InvalidOperationException("لا يمكن تعديل فاتورة لا تحتوي على أصناف.");

        var items = await TvpTableBuilder.CreateAsync(
            _db, "Items_Orders", details.Cast<object>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var contract = await StoredProcedureContract.LoadAsync(
            _db, "dbo.Update_Order_Order_ALL", cancellationToken).ConfigureAwait(false);

        ApplyHeader(contract, existing, branchId, session, items);
        contract.Set("@PurBranchID", purBranchId)
                .SetIfPresent("@UserID_Update", session.UserId)
                .SetIfPresent("@UserBranch_Update", branchId)
                .SetIfPresent("@UserMacAddress_Update", GetMachineMac());

        return await _db.ExecuteStoredProcedureReturnValueAsync(
            "dbo.Update_Order_Order_ALL",
            contract.BuildParameters(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        int invoiceId,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حذف الفواتير يتطلب فرعاً فعّالاً.");

        await _authorization.RequireAsync(
            session, screenId, PermissionAction.Delete, cancellationToken)
            .ConfigureAwait(false);

        var branchId = session.BranchId.Value;
        var row = await _db.QueryAsync(
            @"SELECT TOP (1) PurBranchID
              FROM dbo.Order_Orders
              WHERE (ID = @InvoiceId OR PurBranchID = @InvoiceId)
                AND BranchID = @BranchID;",
            p =>
            {
                p.Add("@InvoiceId", System.Data.SqlClient.SqlDbType.Int).Value = invoiceId;
                p.Add("@BranchID", System.Data.SqlClient.SqlDbType.Int).Value = branchId;
            },
            cancellationToken).ConfigureAwait(false);

        if (row.Rows.Count == 0)
            throw new InvalidOperationException("الفاتورة المطلوب حذفها غير موجودة في الفرع الحالي.");

        var purBranchId = row.Rows[0]["PurBranchID"] == DBNull.Value
            ? invoiceId
            : Convert.ToInt32(row.Rows[0]["PurBranchID"]);

        await _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Delete_Order_Orders",
            p =>
            {
                p.Add("@PurBranchID", System.Data.SqlClient.SqlDbType.Int).Value = purBranchId;
                p.Add("@BranchID", System.Data.SqlClient.SqlDbType.Int).Value = branchId;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(
        AppSession session,
        IReadOnlyList<Order_OrdersDetails> details)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حفظ الفواتير يتطلب فرعاً فعّالاً.");
        if (details.Count == 0)
            throw new ArgumentException("لا يمكن حفظ فاتورة بدون أصناف.");

        foreach (var detail in details)
        {
            if (!detail.ItemID.HasValue || detail.ItemID.Value <= 0)
                throw new ArgumentException("كل سطر في الفاتورة يحتاج إلى صنف صحيح.");
            if (!detail.Quantity.HasValue || detail.Quantity.Value <= 0)
                throw new ArgumentException("كمية الصنف يجب أن تكون أكبر من صفر.");
        }
    }

    private static void ApplyHeader(
        StoredProcedureContract contract,
        Order_Orders invoice,
        int branchId,
        AppSession session,
        DataTable items)
    {
        var machine = GetMachineMac();
        var cashierType = invoice.OrderCashierType ?? false;

        contract
            .Set("@BranchID", branchId)
            .Set("@SupplierID", invoice.SupplierID ?? 0)
            .Set("@IsWaiting", invoice.IsWaiting ?? false)
            .Set("@SupplierName", NullIfEmpty(invoice.SupplierName))
            .Set("@SupplierPhone", NullIfEmpty(invoice.SupplierPhone))
            .Set("@SupplierVatNum", NullIfEmpty(invoice.SupplierVatNum))
            .Set("@Purchases_Date", invoice.Purchases_Date ?? DateTime.Now)
            .Set("@Order_Paymant_Type", invoice.Order_Paymant_Type ?? 1)
            .Set("@Restaurant_PayType", GetString(invoice, "Restaurant_PayType"))
            .Set("@Restaurant_TypeID", GetInt(invoice, "Restaurant_TypeID") ?? (cashierType ? 2 : 1))
            .Set("@CostCentersID", invoice.CostCentersID ?? 0)
            .Set("@BounceID", GetInt(invoice, "BounceID") ?? 0)
            .Set("@Note", NullIfEmpty(invoice.Note))
            .Set("@NoteNum", NullIfEmpty(invoice.NoteNum))
            .Set("@CostOrder", invoice.CostOrder ?? 0m)
            .Set("@Tax", invoice.Tax ?? 0m)
            .Set("@TotalPrices", invoice.TotalPrices ?? 0m)
            .Set("@Safy", invoice.Safy ?? 0m)
            .Set("@DiscountNum", invoice.DiscountNum ?? 0m)
            .Set("@DiscountPerantage", invoice.DiscountPerantage ?? 0m)
            .Set("@HasmPer", GetDecimal(invoice, "HasmPer") ?? 0m)
            .Set("@HasmAmount", GetDecimal(invoice, "HasmAmount") ?? 0m)
            .Set("@Tax_Discount", invoice.Tax_Discount ?? 0m)
            .Set("@TotalPrices_Discount", invoice.TotalPrices_Discount ?? 0m)
            .Set("@TobaccoTax", invoice.TobaccoTax ?? 0m)
            .Set("@AllTax", invoice.AllTax ?? 0m)
            .Set("@Net", invoice.Net ?? 0m)
            .Set("@CashMoney", invoice.CashMoney ?? 0m)
            .Set("@CashBank", invoice.CashBank ?? 0m)
            .Set("@OrderCashierType", cashierType)
            .Set("@ProjectID", GetInt(invoice, "ProjectId") ?? 0)
            .Set("@RoomNum", invoice.RoomNum ?? 0)
            .Set("@TableNum", invoice.TableNum ?? 0)
            .Set("@Sectoral", GetInt(invoice, "Sectoral") ?? 0)
            .Set("@UserID_Add", session.UserId)
            .Set("@UserBranch_Add", branchId)
            .Set("@UserMacAddress_Add", NullIfEmpty(invoice.UserMacAddress_Add) ?? machine)
            .Set("@SalesMan", GetString(invoice, "SalesMan"))
            .Set("@DateHold", GetDateTime(invoice, "DateHold"))
            .Set("@Charge", GetDecimal(invoice, "Charge") ?? 0m)
            .Set("@AllDiscount", GetDecimal(invoice, "AllDiscount") ?? invoice.DiscountNum ?? 0m)
            .Set("@BounsAmount", GetDecimal(invoice, "BounsAmount") ?? 0m)
            .Set("@DisCode", GetString(invoice, "DisCode"))
            .Set("@AmountPaid", GetDecimal(invoice, "AmountPaid") ?? 0m)
            .Set("@Rest", GetDecimal(invoice, "Rest") ?? 0m)
            .Set("@CarName", NullIfEmpty(invoice.CarName))
            .Set("@CarModel", NullIfEmpty(invoice.CarModel))
            .Set("@PlateNumber", NullIfEmpty(invoice.PlateNumber))
            .Set("@ChassisNum", NullIfEmpty(invoice.ChassisNum))
            .Set("@CarColor", NullIfEmpty(invoice.CarColor))
            .Set("@Counter", NullIfEmpty(invoice.Counter))
            .Set("@BuildingNum", NullIfEmpty(invoice.BuildingNum))
            .Set("@Street", NullIfEmpty(invoice.Street))
            .Set("@District", NullIfEmpty(invoice.District))
            .Set("@City", NullIfEmpty(invoice.City))
            .Set("@Country", NullIfEmpty(invoice.Country))
            .Set("@PostalCode", NullIfEmpty(invoice.PostalCode))
            .Set("@AdditionalNum", NullIfEmpty(invoice.AdditionalNum))
            .Set("@CommercialRecord", NullIfEmpty(invoice.CommercialRecord))
            .Set("@Items", items);
    }

    private static void Merge(Order_Orders target, Order_Orders source)
    {
        if (source.SupplierID.HasValue) target.SupplierID = source.SupplierID;
        if (!string.IsNullOrWhiteSpace(source.SupplierName)) target.SupplierName = source.SupplierName;
        if (source.SupplierPhone is not null) target.SupplierPhone = source.SupplierPhone;
        if (source.SupplierVatNum is not null) target.SupplierVatNum = source.SupplierVatNum;
        if (source.Purchases_Date.HasValue) target.Purchases_Date = source.Purchases_Date;
        if (source.Order_Paymant_Type.HasValue) target.Order_Paymant_Type = source.Order_Paymant_Type;
        if (source.CostCentersID.HasValue) target.CostCentersID = source.CostCentersID;
        if (source.Note is not null) target.Note = source.Note;
        if (source.NoteNum is not null) target.NoteNum = source.NoteNum;
        if (source.CostOrder.HasValue) target.CostOrder = source.CostOrder;
        if (source.Tax.HasValue) target.Tax = source.Tax;
        if (source.TotalPrices.HasValue) target.TotalPrices = source.TotalPrices;
        if (source.Safy.HasValue) target.Safy = source.Safy;
        if (source.DiscountNum.HasValue) target.DiscountNum = source.DiscountNum;
        if (source.DiscountPerantage.HasValue) target.DiscountPerantage = source.DiscountPerantage;
        if (source.Tax_Discount.HasValue) target.Tax_Discount = source.Tax_Discount;
        if (source.TotalPrices_Discount.HasValue) target.TotalPrices_Discount = source.TotalPrices_Discount;
        if (source.TobaccoTax.HasValue) target.TobaccoTax = source.TobaccoTax;
        if (source.AllTax.HasValue) target.AllTax = source.AllTax;
        if (source.Net.HasValue) target.Net = source.Net;
        if (source.CashMoney.HasValue) target.CashMoney = source.CashMoney;
        if (source.CashBank.HasValue) target.CashBank = source.CashBank;
        if (source.OrderCashierType.HasValue) target.OrderCashierType = source.OrderCashierType;
        if (source.RoomNum.HasValue) target.RoomNum = source.RoomNum;
        if (source.TableNum.HasValue) target.TableNum = source.TableNum;
    }

    private static List<Order_OrdersDetails> MapDetails(DataTable table, int branchId)
    {
        var result = new List<Order_OrdersDetails>();
        var props = typeof(Order_OrdersDetails).GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (DataRow row in table.Rows)
        {
            var item = new Order_OrdersDetails();
            foreach (DataColumn column in table.Columns)
            {
                if (!props.TryGetValue(column.ColumnName, out var prop))
                    continue;

                var value = row[column];
                if (value == DBNull.Value)
                    continue;

                try
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    prop.SetValue(item, Convert.ChangeType(value, targetType));
                }
                catch
                {
                    try { prop.SetValue(item, value); } catch { }
                }
            }

            item.BranchID = branchId;
            result.Add(item);
        }

        return result;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetString(object value, string property)
        => value.GetType().GetProperty(property)?.GetValue(value)?.ToString();

    private static int? GetInt(object value, string property)
    {
        var raw = value.GetType().GetProperty(property)?.GetValue(value);
        return raw is null ? null : Convert.ToInt32(raw);
    }

    private static decimal? GetDecimal(object value, string property)
    {
        var raw = value.GetType().GetProperty(property)?.GetValue(value);
        return raw is null ? null : Convert.ToDecimal(raw);
    }

    private static DateTime? GetDateTime(object value, string property)
    {
        var raw = value.GetType().GetProperty(property)?.GetValue(value);
        return raw is null ? null : Convert.ToDateTime(raw);
    }

    private static string GetMachineMac()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                .Select(n => n.GetPhysicalAddress()?.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? Environment.MachineName;
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
