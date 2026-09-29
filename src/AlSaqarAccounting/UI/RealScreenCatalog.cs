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
            // شجرة الحسابات — إضافة/تعديل/حذف تشغيلية
            ["FrmAccountTree"] = (cs, s, a) => new AccountsTreeForm(
                s, a, new AccountsTreeService(CreateDb(cs))),
            ["شجرة الحسابات"] = (cs, s, a) => new AccountsTreeForm(
                s, a, new AccountsTreeService(CreateDb(cs))),
            ["الحسابات"] = (cs, s, a) => new AccountsTreeForm(
                s, a, new AccountsTreeService(CreateDb(cs))),

            // العملاء — إضافة/حذف تشغيلية (حساب فرعي تحت الحساب الافتراضي)
            ["FrmCustomer"] = (cs, s, a) => new CustomersForm(
                s, a, new CustSupService(CreateDb(cs)), new AccountsTreeService(CreateDb(cs))),
            ["العملاء"] = (cs, s, a) => new CustomersForm(
                s, a, new CustSupService(CreateDb(cs)), new AccountsTreeService(CreateDb(cs))),

            // الموردون — إضافة/تعديل/حذف تشغيلية
            ["FrmSuppliers"] = (cs, s, a) => new SuppliersForm(
                s, a, new CustSupService(CreateDb(cs))),
            ["الموردون"] = (cs, s, a) => new SuppliersForm(
                s, a, new CustSupService(CreateDb(cs))),

            // الفروع
            ["FrmBranches"] = (cs, s, a) => new BranchesForm(
                s, a, new BranchService(CreateDb(cs))),
            ["الفروع"] = (cs, s, a) => new BranchesForm(
                s, a, new BranchService(CreateDb(cs))),

            // المخازن
            ["FrmStores"] = (cs, s, a) => new StoresForm(
                s, a, new StoresService(CreateDb(cs))),
            ["المخازن"] = (cs, s, a) => new StoresForm(
                s, a, new StoresService(CreateDb(cs))),

            // مندوبو المبيعات
            ["FrmSalesMan"] = (cs, s, a) => new SalesMenForm(
                s, a, new SalesManService(CreateDb(cs))),

            // مراكز التكلفة
            ["FrmCostCenter"] = (cs, s, a) => new CostCentersForm(
                s, a, new DocumentsService(CreateDb(cs))),
            ["FrmCostCenterTree"] = (cs, s, a) => new CostCentersForm(
                s, a, new DocumentsService(CreateDb(cs))),
            ["مراكز التكلفة"] = (cs, s, a) => new CostCentersForm(
                s, a, new DocumentsService(CreateDb(cs))),

            // المشاريع
            ["FrmProjects"] = (cs, s, a) => new ProjectsForm(
                s, a, new DocumentsService(CreateDb(cs))),
            ["المشاريع"] = (cs, s, a) => new ProjectsForm(
                s, a, new DocumentsService(CreateDb(cs))),

            // المبيعات — فاتورة جديدة/حذف تشغيلية عبر Insert_Order_Order_ALL
            ["FrmOrders"] = (cs, s, a) => new OrdersForm(
                s, a,
                new SalesService(CreateDb(cs)),
                new StoresService(CreateDb(cs))),
            ["المبيعات"] = (cs, s, a) => new OrdersForm(
                s, a,
                new SalesService(CreateDb(cs)),
                new StoresService(CreateDb(cs))),

            // المشتريات — فاتورة جديدة/حذف تشغيلية عبر Insert_Order_Purchases
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

            // السندات — سند جديد/حذف تشغيلية عبر Insert_Tran_Tran
            ["FrmReceipts"] = (cs, s, a) => new ReceiptsForm(
                s, a, new VouchersService(CreateDb(cs))),
            ["السندات"] = (cs, s, a) => new ReceiptsForm(
                s, a, new VouchersService(CreateDb(cs)))
        };

    public static bool TryCreate(
        string? screenName,
        string connectionString,
        AppSession session,
        ScreenAccess access,
        out Form? form)
    {
        form = null;
        if (string.IsNullOrWhiteSpace(screenName))
            return false;

        if (!Factories.TryGetValue(screenName.Trim(), out var factory))
            return false;

        form = factory(connectionString, session, access);
        return form is not null;
    }
}
