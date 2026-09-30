using System.Globalization;
using System.Text;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Forms;
using AlSaqarAccounting.Services;

namespace AlSaqarAccounting.UI;

/// <summary>
/// Registry of the migrated real screens. It maps the original screen names
/// (both Frm* names and Arabic names stored in User_Screens) to their concrete
/// Form implementations, so the generic OperationalDataScreen / CatalogDataScreen
/// fallbacks are no longer used for these screens.
/// </summary>
public static class RealScreenCatalog
{
    private delegate Form ScreenFactory(string connectionString, AppSession session, ScreenAccess access);

    private static DbExecutor CreateDb(string connectionString)
        => new(new SqlConnectionFactory(connectionString));

    private static readonly Dictionary<string, ScreenFactory> Factories =
        new(StringComparer.OrdinalIgnoreCase)
            ["FrmAccountTree"] = (cs, s, a) => new AccountsTreeForm(
                s, a, new AccountsTreeService(CreateDb(cs))),

            ["شجرة الحسابات"] = (cs, s, a) => new AccountsTreeForm(
                s, a, new AccountsTreeService(CreateDb(cs))),

            ["الحسابات"] = (cs, s, a) => new AccountsTreeForm(
                s, a, new AccountsTreeService(CreateDb(cs))),

            // العملاء — إضافة/حذف تشغيلية (حساب فرعي تحت الحساب الافتراضي),

            ["FrmCustomer"] = (cs, s, a) => new CustomersForm(
                s, a, new CustSupService(CreateDb(cs)), new AccountsTreeService(CreateDb(cs))),

            ["العملاء"] = (cs, s, a) => new CustomersForm(
                s, a, new CustSupService(CreateDb(cs)), new AccountsTreeService(CreateDb(cs))),

            // الموردون — إضافة/تعديل/حذف تشغيلية,

            ["FrmSuppliers"] = (cs, s, a) => new SuppliersForm(
                s, a, new CustSupService(CreateDb(cs))),

            ["الموردون"] = (cs, s, a) => new SuppliersForm(
                s, a, new CustSupService(CreateDb(cs))),

            // الفروع,

            ["FrmBranches"] = (cs, s, a) => new BranchesForm(
                s, a, new BranchService(CreateDb(cs))),

            ["الفروع"] = (cs, s, a) => new BranchesForm(
                s, a, new BranchService(CreateDb(cs))),

            // المخازن,

            ["FrmStores"] = (cs, s, a) => new StoresForm(
                s, a, new StoresService(CreateDb(cs))),

            ["المخازن"] = (cs, s, a) => new StoresForm(
                s, a, new StoresService(CreateDb(cs))),

            // مندوبو المبيعات,

            ["FrmSalesMan"] = (cs, s, a) => new SalesMenForm(
                s, a, new SalesManService(CreateDb(cs))),

            // مراكز التكلفة,

            ["FrmCostCenter"] = (cs, s, a) => new CostCentersForm(
                s, a, new DocumentsService(CreateDb(cs))),

            ["FrmCostCenterTree"] = (cs, s, a) => new CostCentersForm(
                s, a, new DocumentsService(CreateDb(cs))),

            ["مراكز التكلفة"] = (cs, s, a) => new CostCentersForm(
                s, a, new DocumentsService(CreateDb(cs))),

            // المشاريع,

            ["FrmProjects"] = (cs, s, a) => new ProjectsForm(
                s, a, new DocumentsService(CreateDb(cs))),

            ["المشاريع"] = (cs, s, a) => new ProjectsForm(
                s, a, new DocumentsService(CreateDb(cs))),

            // المبيعات — فاتورة جديدة/حذف تشغيلية عبر Insert_Order_Order_ALL,

            ["FrmOrders"] = (cs, s, a) => new OrdersForm(
                s, a,
                new SalesService(CreateDb(cs)),
                new StoresService(CreateDb(cs))),

            ["المبيعات"] = (cs, s, a) => new OrdersForm(
                s, a,
                new SalesService(CreateDb(cs)),
                new StoresService(CreateDb(cs))),

            // المشتريات — فاتورة جديدة/حذف تشغيلية عبر Insert_Order_Purchases,

            ["FrmPurchases"] = (cs, s, a) => new PurchasesForm(
                s, a,
                new PurchasesService(CreateDb(cs)),
                new StoresService(CreateDb(cs)),
                new CustSupService(CreateDb(cs))),

            ["المشتريات"] = (cs, s, a) => new PurchasesForm(
                s, a,
                new PurchasesService(CreateDb(cs)),
                new StoresService(CreateDb(cs)),
                new CustSupService(CreateDb(cs))),

            // التحويلات والجرد والمرتجعات وعروض الأسعار والضمانات — شاشات حقيقية مرتبطة بإجراءات GTSdb2026,

            ["FrmTransferToBranch"] = (cs, s, a) => new TransferToBranchForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["تحويل إلى فرع"] = (cs, s, a) => new TransferToBranchForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmTransferFromBranch"] = (cs, s, a) => new TransferFromBranchForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["تحويل من فرع"] = (cs, s, a) => new TransferFromBranchForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmStoreTransfer"] = (cs, s, a) => new StoreTransfersForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmStoreTransfers"] = (cs, s, a) => new StoreTransfersForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["تحويلات المخازن"] = (cs, s, a) => new StoreTransfersForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmOrderReturn"] = (cs, s, a) => new SalesReturnsForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["مرتجعات المبيعات"] = (cs, s, a) => new SalesReturnsForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmPurchasesReturn"] = (cs, s, a) => new PurchaseReturnsForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["مرتجعات المشتريات"] = (cs, s, a) => new PurchaseReturnsForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmPriceOffer"] = (cs, s, a) => new PriceOffersForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["عروض الأسعار"] = (cs, s, a) => new PriceOffersForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmGuarantee"] = (cs, s, a) => new GuaranteesForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["ضمانات العقود"] = (cs, s, a) => new GuaranteesForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmGard"] = (cs, s, a) => new InventoryCountForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["الجرد"] = (cs, s, a) => new InventoryCountForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["كميات المخزون"] = (cs, s, a) => new InventoryStockForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmInventoryStock"] = (cs, s, a) => new InventoryStockForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["بونص العقود"] = (cs, s, a) => new ContractBounceForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["FrmContractBounce"] = (cs, s, a) => new ContractBounceForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            // الكميات الافتتاحية — CRUD فعلي عبر Insert/Update/Delete_OpenQuantity + Items_OpenQuantity,

            ["FrmOpenQuantity"] = (cs, s, a) => new OpenQuantitiesForm(
                s, a, new OpenQuantityService(CreateDb(cs))),

            ["الكميات الافتتاحية"] = (cs, s, a) => new OpenQuantitiesForm(
                s, a, new OpenQuantityService(CreateDb(cs))),

            ["كميات افتتاحية"] = (cs, s, a) => new OpenQuantitiesForm(
                s, a, new OpenQuantityService(CreateDb(cs))),

            // تسوية الجرد بالنقص — قراءة وطباعة بعقد موثق,

            ["FrmInventorySettlementMinus"] = (cs, s, a) => new InventorySettlementMinusForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),

            ["تسوية جرد بالنقص"] = (cs, s, a) => new InventorySettlementMinusForm(
                s, a, new InventoryOperationsService(CreateDb(cs))),
            // السندات — سند جديد/حذف تشغيلية عبر Insert_Tran_Tran,

            ["FrmReceipts"] = (cs, s, a) => new ReceiptsForm(
                s, a, new VouchersService(CreateDb(cs))),

            ["السندات"] = (cs, s, a) => new ReceiptsForm(
                s, a, new VouchersService(CreateDb(cs))),

            ["الأصناف"] = (cs, s, a) => new ItemsForm(s, a, new ItemsService(CreateDb(cs))),

            ["الوحدات"] = (cs, s, a) => new ItemUnitForm(s, a, new ItemUnitService(CreateDb(cs))),

            ["الشركات"] = (cs, s, a) => new ItemMasterForm(s, a, new ItemMasterService(CreateDb(cs)), "Item_Company", "الشركات"),

            ["الفئات"] = (cs, s, a) => new ItemMasterForm(s, a, new ItemMasterService(CreateDb(cs)), "Item_Class", "الفئات"),

            ["المجموعات"] = (cs, s, a) => new ItemMasterForm(s, a, new ItemMasterService(CreateDb(cs)), "Item_Groups", "المجموعات"),

            ["FrmContract"] = (cs, s, a) => new ContractsForm(s, a, new ContractService(CreateDb(cs))),

            ["العقود"] = (cs, s, a) => new ContractsForm(s, a, new ContractService(CreateDb(cs))),

            ["المندوبين"] = (cs, s, a) => new SalesMenForm(s, a, new SalesManService(CreateDb(cs))),

            ["المندوبون"] = (cs, s, a) => new SalesMenForm(s, a, new SalesManService(CreateDb(cs))),

            ["المستودعات"] = (cs, s, a) => new StoresForm(s, a, new StoresService(CreateDb(cs))),

            ["الكاشير"] = (cs, s, a) => new CashierForm(s, a, new CashierService(CreateDb(cs)), new ItemsService(CreateDb(cs)), new CustomerService(CreateDb(cs))),

            ["طلب التحويل إلى فرع"] = (cs, s, a) => new TransferToBranchForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["طلب الاستقبال من فرع"] = (cs, s, a) => new TransferFromBranchForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["طلبات التحويل للفروع"] = (cs, s, a) => new StoreTransfersForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["عروض أسعار"] = (cs, s, a) => new PriceOffersForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["مرتجعات المبيعات بفاتورة"] = (cs, s, a) => new SalesReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["مرتجعات المبيعات بدون فاتورة"] = (cs, s, a) => new SalesReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["مرتجعات المشتريات بفاتورة"] = (cs, s, a) => new PurchaseReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["مرتجعات المشتريات بدون فاتورة"] = (cs, s, a) => new PurchaseReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["تسوية الجرد بالنقص"] = (cs, s, a) => new InventorySettlementMinusForm(s, a, new InventoryOperationsService(CreateDb(cs))),

            ["الطابعات"] = (cs, s, a) => new PrinterSettingsForm(s, a, new PrinterSettingsService(CreateDb(cs))),

            ["الطابعة"] = (cs, s, a) => new PrinterSettingsForm(s, a, new PrinterSettingsService(CreateDb(cs))),

            ["إعدادات الطابعات"] = (cs, s, a) => new PrinterSettingsForm(s, a, new PrinterSettingsService(CreateDb(cs))),

            ["إعدادات طابعة الكاشير"] = (cs, s, a) => new PrinterSettingsForm(s, a, new PrinterSettingsService(CreateDb(cs))),

            ["طابعات الكاشير"] = (cs, s, a) => new PrinterSettingsForm(s, a, new PrinterSettingsService(CreateDb(cs))),

            ["طابعات المطبخ"] = (cs, s, a) => new PrinterSettingsForm(s, a, new PrinterSettingsService(CreateDb(cs))),

            ["طبعات المطبخ"] = (cs, s, a) => new PrinterSettingsForm(s, a, new PrinterSettingsService(CreateDb(cs))),

            ["إعدادات طابعات المطبخ"] = (cs, s, a) => new PrinterSettingsForm(s, a, new PrinterSettingsService(CreateDb(cs))),
        };

    public static bool TryCreate(string? screenName, string connectionString, AppSession session, ScreenAccess access, out Form? form)
    {
        form = null;
        var raw = screenName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // First honor the exact database label. This keeps all existing Arabic mappings intact.
        if (Factories.TryGetValue(raw, out var factory))
        {
            form = factory(connectionString, session, access);
            return form is not null;
        }

        var normalized = NormalizeScreenName(raw);

        // The original database can contain hidden Unicode formatting characters or minor
        // spelling variants. Resolve migrated screens semantically instead of falling back.
        if (IsItemScreen(normalized))
        {
            form = new ItemsForm(session, access, new ItemsService(CreateDb(connectionString)));
            return true;
        }

        if (IsUnitScreen(normalized))
        {
            form = new ItemUnitForm(session, access, new ItemUnitService(CreateDb(connectionString)));
            return true;
        }

        // Resolve common legacy screen-name variants to the concrete ERP Forms
        // already implemented in this repository. This prevents ordinary Arabic
        // labels, Frm* aliases and plural/suffix variants from reaching the
        // generic DynamicErpScreenForm.
        var db = CreateDb(connectionString);

        if (normalized.Contains("عميل", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("عملاء", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("customer", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("cust", StringComparison.OrdinalIgnoreCase))
        {
            form = new CustomersForm(session, access, new CustSupService(db), new AccountsTreeService(db));
            return true;
        }

        if (normalized.Contains("مورد", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("موردون", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("supplier", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("supp", StringComparison.OrdinalIgnoreCase))
        {
            form = new SuppliersForm(session, access, new CustSupService(db));
            return true;
        }

        if (normalized.Contains("فرع", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("branch", StringComparison.OrdinalIgnoreCase))
        {
            form = new BranchesForm(session, access, new BranchService(db));
            return true;
        }

        if (normalized.Contains("مخزن", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("مستودع", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("store", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("warehouse", StringComparison.OrdinalIgnoreCase))
        {
            form = new StoresForm(session, access, new StoresService(db));
            return true;
        }

        if (normalized.Contains("مندوب", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("salesman", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("salesman", StringComparison.OrdinalIgnoreCase))
        {
            form = new SalesMenForm(session, access, new SalesManService(db));
            return true;
        }

        if (normalized.Contains("مركزالتكلفة", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("مراكزالتكلفة", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("costcenter", StringComparison.OrdinalIgnoreCase))
        {
            form = new CostCentersForm(session, access, new DocumentsService(db));
            return true;
        }

        if (normalized.Contains("مشروع", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("project", StringComparison.OrdinalIgnoreCase))
        {
            form = new ProjectsForm(session, access, new DocumentsService(db));
            return true;
        }

        if (normalized.Contains("عقد", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("عقود", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("contract", StringComparison.OrdinalIgnoreCase))
        {
            form = new ContractsForm(session, access, new ContractService(db));
            return true;
        }

        if (normalized.Contains("شراء", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("مشتريات", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("purchase", StringComparison.OrdinalIgnoreCase))
        {
            form = new PurchasesForm(session, access, new PurchasesService(db), new StoresService(db), new CustSupService(db));
            return true;
        }

        if (normalized.Contains("بيع", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("مبيعات", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("order", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("sales", StringComparison.OrdinalIgnoreCase))
        {
            form = new OrdersForm(session, access, new SalesService(db), new StoresService(db));
            return true;
        }

        if (normalized.Contains("فاتورة", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("فواتير", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("invoice", StringComparison.OrdinalIgnoreCase))
        {
            form = new InvoicesForm(
                session,
                access,
                new InvoiceService(db),
                new CustomerService(db),
                new SupplierService(db),
                new ItemsService(db));
            return true;
        }

        if (normalized.Contains("سند", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("قبض", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("صرف", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("voucher", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("receipt", StringComparison.OrdinalIgnoreCase))
        {
            form = new ReceiptsForm(session, access, new VouchersService(db));
            return true;
        }

        if ((normalized.Contains("حساب", StringComparison.OrdinalIgnoreCase) ||
             normalized.Contains("account", StringComparison.OrdinalIgnoreCase)) &&
            (normalized.Contains("شجر", StringComparison.OrdinalIgnoreCase) ||
             normalized.Contains("tree", StringComparison.OrdinalIgnoreCase)))
        {
            form = new AccountsTreeForm(session, access, new AccountsTreeService(db));
            return true;
        }

        return false;
    }

    private static bool IsItemScreen(string normalized)
        => normalized.Contains("الاصناف", StringComparison.OrdinalIgnoreCase) ||
           normalized.Contains("اصناف", StringComparison.OrdinalIgnoreCase) ||
           normalized.Contains("الصنف", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("item", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("items", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("frmitms", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("frmitems", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnitScreen(string normalized)
        => normalized.Contains("الوحدات", StringComparison.OrdinalIgnoreCase) ||
           normalized.Contains("وحدات", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("frmunit", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeScreenName(string value)
    {
        var form = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.Format ||
                category == UnicodeCategory.NonSpacingMark ||
                category == UnicodeCategory.SpacingCombiningMark ||
                char.IsWhiteSpace(ch))
                continue;

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
