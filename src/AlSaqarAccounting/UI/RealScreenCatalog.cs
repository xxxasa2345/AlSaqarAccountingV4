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
        {
            ["الأصناف"] = (cs, s, a) => new ItemsForm(s, a, new ItemsService(CreateDb(cs))),
            ["الوحدات"] = (cs, s, a) => new ItemUnitForm(s, a, new ItemUnitService(CreateDb(cs))),
            ["الشركات"] = (cs, s, a) => new ItemMasterForm(s, a, new ItemMasterService(CreateDb(cs)), "Item_Company", "الشركات"),
            ["الفئات"] = (cs, s, a) => new ItemMasterForm(s, a, new ItemMasterService(CreateDb(cs)), "Item_Class", "الفئات"),
            ["المجموعات"] = (cs, s, a) => new ItemMasterForm(s, a, new ItemMasterService(CreateDb(cs)), "Item_Groups", "المجموعات"),

            ["FrmAccountTree"] = (cs, s, a) => new AccountsTreeForm(s, a, new AccountsTreeService(CreateDb(cs))),
            ["شجرة الحسابات"] = (cs, s, a) => new AccountsTreeForm(s, a, new AccountsTreeService(CreateDb(cs))),
            ["الحسابات"] = (cs, s, a) => new AccountsTreeForm(s, a, new AccountsTreeService(CreateDb(cs))),
            ["FrmCustomer"] = (cs, s, a) => new CustomersForm(s, a, new CustSupService(CreateDb(cs)), new AccountsTreeService(CreateDb(cs))),
            ["العملاء"] = (cs, s, a) => new CustomersForm(s, a, new CustSupService(CreateDb(cs)), new AccountsTreeService(CreateDb(cs))),
            ["FrmSuppliers"] = (cs, s, a) => new SuppliersForm(s, a, new CustSupService(CreateDb(cs))),
            ["الموردون"] = (cs, s, a) => new SuppliersForm(s, a, new CustSupService(CreateDb(cs))),
            ["FrmBranches"] = (cs, s, a) => new BranchesForm(s, a, new BranchService(CreateDb(cs))),
            ["الفروع"] = (cs, s, a) => new BranchesForm(s, a, new BranchService(CreateDb(cs))),
            ["FrmStores"] = (cs, s, a) => new StoresForm(s, a, new StoresService(CreateDb(cs))),
            ["المخازن"] = (cs, s, a) => new StoresForm(s, a, new StoresService(CreateDb(cs))),
            ["FrmSalesMan"] = (cs, s, a) => new SalesMenForm(s, a, new SalesManService(CreateDb(cs))),
            ["FrmCostCenter"] = (cs, s, a) => new CostCentersForm(s, a, new DocumentsService(CreateDb(cs))),
            ["FrmCostCenterTree"] = (cs, s, a) => new CostCentersForm(s, a, new DocumentsService(CreateDb(cs))),
            ["مراكز التكلفة"] = (cs, s, a) => new CostCentersForm(s, a, new DocumentsService(CreateDb(cs))),
            ["FrmContract"] = (cs, s, a) => new ContractsForm(s, a, new ContractService(CreateDb(cs))),
            ["العقود"] = (cs, s, a) => new ContractsForm(s, a, new ContractService(CreateDb(cs))),
            ["المندوبين"] = (cs, s, a) => new SalesMenForm(s, a, new SalesManService(CreateDb(cs))),
            ["المندوبون"] = (cs, s, a) => new SalesMenForm(s, a, new SalesManService(CreateDb(cs))),
            ["المستودعات"] = (cs, s, a) => new StoresForm(s, a, new StoresService(CreateDb(cs))),
            ["الكاشير"] = (cs, s, a) => new CashierForm(s, a, new CashierService(CreateDb(cs)), new ItemsService(CreateDb(cs)), new CustomerService(CreateDb(cs))),
            ["FrmProjects"] = (cs, s, a) => new ProjectsForm(s, a, new DocumentsService(CreateDb(cs))),
            ["المشاريع"] = (cs, s, a) => new ProjectsForm(s, a, new DocumentsService(CreateDb(cs))),
            ["FrmOrders"] = (cs, s, a) => new OrdersForm(s, a, new SalesService(CreateDb(cs)), new StoresService(CreateDb(cs))),
            ["المبيعات"] = (cs, s, a) => new OrdersForm(s, a, new SalesService(CreateDb(cs)), new StoresService(CreateDb(cs))),
            ["FrmPurchases"] = (cs, s, a) => new PurchasesForm(s, a, new PurchasesService(CreateDb(cs)), new StoresService(CreateDb(cs)), new CustSupService(CreateDb(cs))),
            ["المشتريات"] = (cs, s, a) => new PurchasesForm(s, a, new PurchasesService(CreateDb(cs)), new StoresService(CreateDb(cs)), new CustSupService(CreateDb(cs))),
            ["طلب التحويل إلى فرع"] = (cs, s, a) => new TransferToBranchForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["طلب الاستقبال من فرع"] = (cs, s, a) => new TransferFromBranchForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["طلبات التحويل للفروع"] = (cs, s, a) => new StoreTransfersForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["عروض أسعار"] = (cs, s, a) => new PriceOffersForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["مرتجعات المبيعات بفاتورة"] = (cs, s, a) => new SalesReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["مرتجعات المبيعات بدون فاتورة"] = (cs, s, a) => new SalesReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["مرتجعات المشتريات بفاتورة"] = (cs, s, a) => new PurchaseReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["مرتجعات المشتريات بدون فاتورة"] = (cs, s, a) => new PurchaseReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["تسوية الجرد بالنقص"] = (cs, s, a) => new InventorySettlementMinusForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["الكميات الافتتاحية"] = (cs, s, a) => new OpenQuantitiesForm(s, a, new OpenQuantityService(CreateDb(cs))),
            ["FrmTransferToBranch"] = (cs, s, a) => new TransferToBranchForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["تحويل إلى فرع"] = (cs, s, a) => new TransferToBranchForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmTransferFromBranch"] = (cs, s, a) => new TransferFromBranchForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["تحويل من فرع"] = (cs, s, a) => new TransferFromBranchForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmStoreTransfer"] = (cs, s, a) => new StoreTransfersForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmStoreTransfers"] = (cs, s, a) => new StoreTransfersForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["تحويلات المخازن"] = (cs, s, a) => new StoreTransfersForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmOrderReturn"] = (cs, s, a) => new SalesReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["مرتجعات المبيعات"] = (cs, s, a) => new SalesReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmPurchasesReturn"] = (cs, s, a) => new PurchaseReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["مرتجعات المشتريات"] = (cs, s, a) => new PurchaseReturnsForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmPriceOffer"] = (cs, s, a) => new PriceOffersForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["عروض الأسعار"] = (cs, s, a) => new PriceOffersForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmGuarantee"] = (cs, s, a) => new GuaranteesForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["ضمانات العقود"] = (cs, s, a) => new GuaranteesForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmGard"] = (cs, s, a) => new InventoryCountForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["الجرد"] = (cs, s, a) => new InventoryCountForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["كميات المخزون"] = (cs, s, a) => new InventoryStockForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmInventoryStock"] = (cs, s, a) => new InventoryStockForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["بونص العقود"] = (cs, s, a) => new ContractBounceForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmContractBounce"] = (cs, s, a) => new ContractBounceForm(s, a, new InventoryOperationsService(CreateDb(cs))),
            ["FrmReceipts"] = (cs, s, a) => new ReceiptsForm(s, a, new VouchersService(CreateDb(cs))),
            ["السندات"] = (cs, s, a) => new ReceiptsForm(s, a, new VouchersService(CreateDb(cs)))
        };

    public static bool TryCreate(
        string? screenName,
        string connectionString,
        AppSession session,
        ScreenAccess access,
        out Form? form)
    {
        form = null;
        var normalized = NormalizeScreenName(screenName);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (Factories.TryGetValue(normalized, out var factory))
        {
            form = factory(connectionString, session, access);
            return form is not null;
        }

        // The original database sometimes contains Arabic labels with hidden
        // Unicode formatting characters or minor spelling variants. Resolve the
        // canonical migrated forms from the normalized semantic name instead of
        // falling through to the generic dynamic screen.
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

    private static string NormalizeScreenName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

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
                'ة' => 'ه',
                _ => ch
            });
        }

        return builder.ToString();
    }
}
