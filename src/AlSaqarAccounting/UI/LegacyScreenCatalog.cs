using System;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.UI;

/// <summary>
/// Additional concrete bindings for legacy ERP screen names whose verified
/// GTSdb2026 SELECT procedures are already present in the repository catalog.
/// </summary>
public static class LegacyScreenCatalog
{
    public static bool TryCreate(
        string? screenName,
        string connectionString,
        AppSession session,
        ScreenAccess access,
        out Form? form)
    {
        form = null;

        var name = Normalize(screenName);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        string? title = null;
        string? procedure = null;
        var mode = LegacyOperationMode.Branch;

        switch (name)
        {
            case "FrmPayment":
            case "FrmPaymentItem":
            case "FrmPaymentBig":
            case "المدفوعات":
            case "دفعات الأصناف":
                title = "مدفوعات الأصناف";
                procedure = "dbo.Select_Order_PaymentItem";
                break;

            case "FrmRecpItem":
            case "FrmRecItem":
            case "الاستلام من الأصناف":
                title = "استلام الأصناف";
                procedure = "dbo.Select_Order_RecItem";
                break;

            case "FrmDalala":
            case "الدلالة":
                title = "الدلالة";
                procedure = "dbo.Select_OrderDalala_Dalala";
                break;

            case "FrmSending":
            case "الإرسال":
                title = "الإرسال";
                procedure = "dbo.Select_OrderDalala_Sending";
                break;

            case "FrmManufacturing":
            case "التصنيع":
                title = "التصنيع";
                procedure = "dbo.Select_Order_Manufacturing";
                break;

            case "FrmManufacturingOrder":
            case "أوامر التصنيع":
                title = "أوامر التصنيع";
                procedure = "dbo.Select_Order_ManufacturingOrder";
                break;

            case "FrmRent":
            case "الإيجارات":
                title = "الإيجارات";
                procedure = "dbo.Select_OrderRent_Rent";
                break;

            case "FrmCheckOut":
            case "FrmCheckout":
            case "التحصيل":
            case "تحصيل الطلبات":
                title = "تحصيل الطلبات";
                mode = LegacyOperationMode.Checkout;
                break;

            case "FrmEntryPermission":
            case "إذن دخول السقالات":
                title = "إذن دخول السقالات";
                procedure = "dbo.Select_Scaffold_EntryPermission";
                break;

            case "FrmExitPermission":
            case "إذن خروج السقالات":
                title = "إذن خروج السقالات";
                procedure = "dbo.Select_Scaffold_ExitPermission";
                break;

            case "FrmDelivery":
            case "التوصيل":
            case "طلبات التوصيل":
                title = "التوصيل";
                mode = LegacyOperationMode.RestaurantDelivery;
                break;

            case "FrmCashirPharm":
            case "FrmCashirWashing":
            case "FrmCashirWithOutGroup":
                title = "نقطة البيع";
                form = new AlSaqarAccounting.Forms.CashierForm(
                    session,
                    access,
                    new CashierService(new Core.DbExecutor(new Core.SqlConnectionFactory(connectionString))),
                    new ItemsService(new Core.DbExecutor(new Core.SqlConnectionFactory(connectionString))),
                    new CustomerService(new Core.DbExecutor(new Core.SqlConnectionFactory(connectionString))));
                return true;

            case "FrmOrdersReturnNoPurchCode":
            case "مرتجع مبيعات بدون فاتورة":
                title = "مرتجعات المبيعات";
                form = new AlSaqarAccounting.Forms.SalesReturnsForm(
                    session,
                    access,
                    new InventoryOperationsService(new Core.DbExecutor(new Core.SqlConnectionFactory(connectionString))));
                return true;

            case "FrmPurchasesReturnNoPurchCode":
            case "مرتجع مشتريات بدون فاتورة":
                title = "مرتجعات المشتريات";
                form = new AlSaqarAccounting.Forms.PurchaseReturnsForm(
                    session,
                    access,
                    new InventoryOperationsService(new Core.DbExecutor(new Core.SqlConnectionFactory(connectionString))));
                return true;

            default:
                return false;
        }

        var db = new Core.DbExecutor(new Core.SqlConnectionFactory(connectionString));
        form = new AlSaqarAccounting.Forms.LegacyOperationalForm(
            session,
            access,
            new LegacyOperationalService(db),
            title!,
            procedure ?? string.Empty,
            mode);

        return true;
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty)
            .Trim()
            .Replace("\u200E", string.Empty)
            .Replace("\u200F", string.Empty)
            .Replace("\u202A", string.Empty)
            .Replace("\u202B", string.Empty)
            .Replace("\u202C", string.Empty);
}
