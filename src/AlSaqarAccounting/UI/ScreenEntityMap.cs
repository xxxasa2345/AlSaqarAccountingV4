using System.Globalization;
using System.Text;

namespace AlSaqarAccounting.UI;

public static class ScreenEntityMap
{
    private static readonly Dictionary<string, string> Exact =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FrmAccountTree"] = "Account_Accounts",
            ["FrmCardAccount"] = "Account_Accounts",
            ["FrmCostCenter"] = "Account_CostCenters",
            ["FrmCostCenterTree"] = "Account_CostCenters",
            ["FrmDefualtAccount"] = "Account_DefualtAccount",
            ["FrmDefualtCustomer"] = "Account_DefualtCustomer",
            ["FrmPayment"] = "Order_PaymentItem",
            ["FrmReceipts"] = "Account_Receipts",
            ["FrmProjects"] = "Account_Projects",
            ["FrmBranches"] = "Account_Branch",
            ["FrmCustomer"] = "Account_DefualtCustomer",
            ["FrmSuppliers"] = "Account_CustSup",
            ["FrmStores"] = "Account_Stores",
            ["FrmSalesMan"] = "Account_SalesMan",
            ["FrmPlace"] = "Account_Place",
            ["FrmReservation"] = "Order_Reservation",
            ["FrmItems"] = "Item_Items",
            ["FrmUnit"] = "Item_Unit",
            ["FrmClass"] = "Item_Class",
            ["FrmCompany"] = "Item_Company",
            ["FrmCountry"] = "Item_Country",
            ["FrmDoctor"] = "Item_Doctor",
            ["FrmGuarantee"] = "Contract_Guarantee",
            ["FrmDalala"] = "OrderDalala_Dalala",
            ["FrmSending"] = "OrderDalala_Sending",
            ["FrmManufacturing"] = "Order_Manufacturing",
            ["FrmManufacturingOrder"] = "Order_ManufacturingOrder",
            ["FrmRent"] = "OrderRent_Rent",
            ["FrmRecipt"] = "OrderRent_Recipt",
            ["FrmRecipt3"] = "OrderRent_Recipt3",
            ["FrmRentalExpenses"] = "RentalExpenses",
            ["FrmRentalInvoice"] = "Orders_RentalInvoice",
            ["FrmContract"] = "Contract_Contract",
            ["FrmEJAR"] = "Contract_EJAR",
            ["FrmEntryPermission"] = "Scaffold_EntryPermission",
            ["FrmExitPermission"] = "Scaffold_ExitPermission",
            ["FrmEmployee"] = "Emp_Employee",
            ["FrmPayroll"] = "Emp_Payroll",
            ["FrmRepairs"] = "Repairs_Repair",
            ["FrmRepair"] = "Repairs_Repair",
            ["FrmDelivery"] = "Restaurant_Delivery",
            ["FrmRoom"] = "Restaurant_Room",
            ["FrmTable"] = "Restaurant_Table",
            ["FrmZatcaIntgration"] = "ResultElectronicInvoiceXmls",

            ["الأصناف"] = "Item_Items",
            ["الصنف"] = "Item_Items",
            ["الوحدات"] = "Item_Unit",
            ["الشركات"] = "Item_Company",
            ["الفئات"] = "Item_Class",
            ["المجموعات"] = "Item_Groups",
            ["الحسابات"] = "Account_Accounts",
            ["شجرة الحسابات"] = "Account_Accounts",
            ["العملاء"] = "Account_DefualtCustomer",
            ["الموردين"] = "Account_CustSup",
            ["الموردون"] = "Account_CustSup",
            ["الفروع"] = "Account_Branch",
            ["المخازن"] = "Account_Stores",
            ["المستودعات"] = "Account_Stores",
            ["المندوبين"] = "Account_SalesMan",
            ["المندوبون"] = "Account_SalesMan",
            ["مراكز التكلفة"] = "Account_CostCenters",
            ["المشاريع"] = "Account_Projects",
            ["العقود"] = "Contract_Contract",
            ["ضمانات العقود"] = "Contract_Guarantee",
            ["المبيعات"] = "Order_Orders",
            ["المشتريات"] = "Order_Purchases",
            ["مرتجعات المبيعات"] = "Order_OrderReturn",
            ["مرتجعات المبيعات بفاتورة"] = "Order_OrderReturn",
            ["مرتجعات المبيعات بدون فاتورة"] = "Order_OrderReturn",
            ["مرتجعات المشتريات"] = "Order_PurchasesReturn",
            ["مرتجعات المشتريات بفاتورة"] = "Order_PurchasesReturn",
            ["مرتجعات المشتريات بدون فاتورة"] = "Order_PurchasesReturn",
            ["أمر صرف للمخازن"] = "Order_PaymentItem",
            ["أمر توريد للمخازن"] = "Order_RecItem",
            ["أمر توريد المخازن"] = "Order_RecItem",
            ["تحويل إلى فرع"] = "Order_TransferToBranch",
            ["طلب التحويل إلى فرع"] = "Order_TransferToBranch",
            ["تحويل من فرع"] = "Order_TransferFromBranch",
            ["طلب الاستقبال من فرع"] = "Order_TransferFromBranch",
            ["طلبات التحويل للفروع"] = "Order_TransferToBranch",
            ["تحويلات المخازن"] = "Order_StoreTransfer",
            ["عروض أسعار"] = "Order_PriceOffer",
            ["عروض الأسعار"] = "Order_PriceOffer",
            ["الجرد"] = "Order_Gard",
            ["كميات المخزون"] = "ItemQuantity",
            ["الكميات الافتتاحية"] = "Order_OpenQuantity",
            ["بونص العقود"] = "Contract_Bounce",

            ["الطابعات"] = "Printers",
            ["الطابعة"] = "Printers",
            ["إعدادات الطابعات"] = "Printers",
            ["إعدادات طابعة الكاشير"] = "PrintersCook",
            ["طابعات الكاشير"] = "PrintersCook",
            ["طابعات المطبخ"] = "PrintersCook",
            ["طبعات المطبخ"] = "PrintersCook",
            ["إعدادات طابعات المطبخ"] = "PrintersCook"
        };

    public static string? Resolve(string screenName)
    {
        if (string.IsNullOrWhiteSpace(screenName)) return null;
        var raw = screenName.Trim();
        if (Exact.TryGetValue(raw, out var table)) return table;
        var n = Normalize(raw);

        if (n.Contains("اعداداتطابعةالكاشير", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("طابعاتالكاشير", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("طابعاتالمطبخ", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("طبعاتالمطبخ", StringComparison.OrdinalIgnoreCase)) return "PrintersCook";
        if (n.Contains("طابعات", StringComparison.OrdinalIgnoreCase) || n.Contains("طابعة", StringComparison.OrdinalIgnoreCase)) return "Printers";
        if (n.Contains("اصناف", StringComparison.OrdinalIgnoreCase) || n.Contains("الصنف", StringComparison.OrdinalIgnoreCase)) return "Item_Items";
        if (n.Contains("وحدات", StringComparison.OrdinalIgnoreCase) || n.Contains("الوحدات", StringComparison.OrdinalIgnoreCase)) return "Item_Unit";
        if (n.Contains("شركة", StringComparison.OrdinalIgnoreCase) || n.Contains("شركات", StringComparison.OrdinalIgnoreCase)) return "Item_Company";
        if (n.Contains("فئة", StringComparison.OrdinalIgnoreCase) || n.Contains("فئات", StringComparison.OrdinalIgnoreCase)) return "Item_Class";
        if (n.Contains("مجموعة", StringComparison.OrdinalIgnoreCase) || n.Contains("مجموعات", StringComparison.OrdinalIgnoreCase)) return "Item_Groups";
        if (n.Contains("عميل", StringComparison.OrdinalIgnoreCase) || n.Contains("عملاء", StringComparison.OrdinalIgnoreCase)) return "Account_DefualtCustomer";
        if (n.Contains("مورد", StringComparison.OrdinalIgnoreCase) || n.Contains("موردين", StringComparison.OrdinalIgnoreCase)) return "Account_CustSup";
        if (n.Contains("مخزن", StringComparison.OrdinalIgnoreCase) || n.Contains("مخازن", StringComparison.OrdinalIgnoreCase) || n.Contains("مستودع", StringComparison.OrdinalIgnoreCase)) return "Account_Stores";
        if (n.Contains("فرع", StringComparison.OrdinalIgnoreCase) || n.Contains("فروع", StringComparison.OrdinalIgnoreCase)) return "Account_Branch";
        if (n.Contains("مندوب", StringComparison.OrdinalIgnoreCase)) return "Account_SalesMan";
        if (n.Contains("حساب", StringComparison.OrdinalIgnoreCase)) return "Account_Accounts";
        if (n.Contains("مركزتكلفة", StringComparison.OrdinalIgnoreCase)) return "Account_CostCenters";
        if (n.Contains("مشروع", StringComparison.OrdinalIgnoreCase)) return "Account_Projects";
        if (n.Contains("عقد", StringComparison.OrdinalIgnoreCase)) return "Contract_Contract";
        if (n.Contains("ضمان", StringComparison.OrdinalIgnoreCase)) return "Contract_Guarantee";
        if (n.Contains("توريد") && n.Contains("مخزن")) return "Order_RecItem";
        if (n.Contains("صرف") && n.Contains("مخزن")) return "Order_PaymentItem";
        if (n.Contains("تحويل") && n.Contains("فرع") && n.Contains("من")) return "Order_TransferFromBranch";
        if (n.Contains("تحويل") && n.Contains("فرع")) return "Order_TransferToBranch";
        if (n.Contains("تحويل") && n.Contains("مخزن")) return "Order_StoreTransfer";
        if (n.Contains("مرتجع") && n.Contains("مبيعات")) return "Order_OrderReturn";
        if (n.Contains("مرتجع") && n.Contains("مشتريات")) return "Order_PurchasesReturn";
        if (n.Contains("مشتريات")) return "Order_Purchases";
        if (n.Contains("مبيعات")) return "Order_Orders";
        if (n.Contains("جرد")) return "Order_Gard";
        if (n.Contains("افتتاح") && n.Contains("كم")) return "Order_OpenQuantity";
        return null;
    }

    private static string Normalize(string value)
    {
        var form = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.Format || category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark || char.IsWhiteSpace(ch)) continue;
            builder.Append(ch switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                _ => ch
            });
        }
        return builder.ToString();
    }
}
