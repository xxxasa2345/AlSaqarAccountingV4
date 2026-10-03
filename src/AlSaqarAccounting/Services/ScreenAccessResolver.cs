using AlSaqarAccounting.Core;
using AlSaqarAccounting.UI;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Resolves a related ERP screen against the current user's actual
/// User_Screens/User_Permission rows before ScreenRouter opens it.
/// Legacy aliases are supported because the original ERP uses mixed Arabic
/// captions and WinForms class names for the same operational screen.
/// </summary>
public sealed class ScreenAccessResolver
{
    private readonly SecurityService _security;

    public ScreenAccessResolver(SqlConnectionFactory factory)
        => _security = new SecurityService(factory);

    public async Task<ScreenAccess?> GetAsync(
        AppSession session,
        string screenName,
        CancellationToken cancellationToken = default)
    {
        var normalized = screenName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var aliases = normalized switch
        {
            "الموردون" => new[] { "الموردون", "الموردين", "FrmSuppliers" },
            "المخازن" => new[] { "المخازن", "FrmStores", "StoresForm" },
            "المبيعات" => new[] { "المبيعات", "فواتير المبيعات", "FrmOrders", "OrdersForm" },
            "المشتريات" => new[] { "المشتريات", "فواتير المشتريات", "FrmPurchases", "PurchasesForm" },
            "السندات" => new[] { "السندات", "سندات القبض", "FrmReceipts", "ReceiptsForm" },
            "شجرة الحسابات" => new[] { "شجرة الحسابات", "الحسابات", "FrmAccountTree", "AccountsTreeForm" },
            "العملاء" => new[] { "العملاء", "العملاء والموردون", "FrmCustomers", "CustomersForm" },
            "مرتجعات المبيعات بفاتورة" => new[] { "مرتجعات المبيعات بفاتورة", "مرتجعات المبيعات", "FrmOrdersReturn", "FrmOrderReturn" },
            "مرتجعات المشتريات بفاتورة" => new[] { "مرتجعات المشتريات بفاتورة", "مرتجعات المشتريات", "FrmPurchasesReturn" },
            "طلب التحويل إلى فرع" => new[] { "طلب التحويل إلى فرع", "تحويل إلى فرع", "FrmTransferToBranch", "TransferToBranchForm" },
            "الكميات الافتتاحية" => new[] { "الكميات الافتتاحية", "كميات افتتاحية", "FrmOpenQuantity", "OpenQuantitiesForm" },
            _ => new[] { normalized }
        };

        var screens = await _security.GetAccessibleScreensAsync(
            session, cancellationToken).ConfigureAwait(false);

        return screens.FirstOrDefault(s =>
            aliases.Any(alias =>
                string.Equals(s.ScreenName?.Trim(), alias, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<ScreenAccess> RequireAsync(
        AppSession session,
        string screenName,
        CancellationToken cancellationToken = default)
        => await GetAsync(session, screenName, cancellationToken).ConfigureAwait(false)
           ?? throw new UnauthorizedAccessException(
               $"لا تملك صلاحية فتح الشاشة «{screenName}».");
}
