using System.Reflection;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.Forms;

namespace AlSaqarAccounting.UI;

/// <summary>
/// Routes a permitted screen to its concrete implementation. Generic catalog
/// screens remain a compatibility fallback, not the primary ERP implementation.
/// </summary>
public sealed class ScreenRouter
{
    private readonly string _connectionString;
    private readonly AppSession _session;

    public ScreenRouter(string connectionString, AppSession session)
    {
        _connectionString = connectionString;
        _session = session;
    }

    public bool TryOpen(Form owner, ScreenAccess access, out string message)
    {
        message = string.Empty;

        if (!access.AllowEnter)
        {
            message = "لا تملك صلاحية فتح هذه الشاشة.";
            return false;
        }

        var db = new DbExecutor(new SqlConnectionFactory(_connectionString));

        if (string.Equals(access.ScreenName, "FrmUnit", StringComparison.OrdinalIgnoreCase))
        {
            var service = new ItemUnitService(db);
            using var form = new ItemUnitForm(_session, access, service);
            form.StartPosition = FormStartPosition.CenterParent;
            form.ShowDialog(owner);
            return true;
        }

        if (TryResolveItemMaster(access.ScreenName, out var tableName, out var displayName))
        {
            var service = new ItemMasterService(db);
            using var form = new ItemMasterForm(_session, access, service, tableName, displayName);
            form.StartPosition = FormStartPosition.CenterParent;
            form.ShowDialog(owner);
            return true;
        }

        // Concrete inventory item screen: original Get_All_Items / Insert_Items.
        if (string.Equals(access.ScreenName, "FrmItems", StringComparison.OrdinalIgnoreCase))
        {
            var service = new ItemsService(db);
            using var form = new ItemsForm(_session, access, service);
            form.StartPosition = FormStartPosition.CenterParent;
            form.ShowDialog(owner);
            return true;
        }

        // Legacy invoice screen remains available for the Arabic "الفواتير" entry.
        if (string.Equals(access.ScreenName, "InvoicesForm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(access.ScreenName, "الفواتير", StringComparison.OrdinalIgnoreCase))
        {
            var invoiceService = new InvoiceService(db);
            var customerService = new CustomerService(db);
            var supplierService = new SupplierService(db);
            var itemsService = new ItemsService(db);
            using var form = new InvoicesForm(
                _session,
                access,
                invoiceService,
                customerService,
                supplierService,
                itemsService);
            form.StartPosition = FormStartPosition.CenterParent;
            form.ShowDialog(owner);
            return true;
        }

        // Migrated real screens (accounts tree, customers, suppliers, branches,
        // stores, salesmen, cost centers, projects, orders, purchases, receipts).
        if (RealScreenCatalog.TryCreate(access.ScreenName, _connectionString, _session, access, out var realScreen) &&
            realScreen is not null)
        {
            using (realScreen)
            {
                realScreen.StartPosition = FormStartPosition.CenterParent;
                realScreen.ShowDialog(owner);
            }
            return true;
        }

        if (OperationalScreenRegistry.TryResolve(access.ScreenName, out var operational))
        {
            var mappedType = operational.TargetFormName is null
                ? null
                : FindFormType(operational.TargetFormName);

            if (mappedType is not null &&
                TryCreateForm(mappedType, access, out var mappedForm, out _) &&
                mappedForm is not null)
            {
                using (mappedForm)
                {
                    mappedForm.StartPosition = FormStartPosition.CenterParent;
                    mappedForm.ShowDialog(owner);
                }
                return true;
            }

            using var operationalScreen = new OperationalDataScreen(
                _connectionString,
                operational,
                access,
                _session.BranchId);
            operationalScreen.ShowDialog(owner);
            return true;
        }

        var type = FindFormType(access.ScreenName);
        if (type is not null)
        {
            if (TryCreateForm(type, access, out var form, out var formError) && form is not null)
            {
                using (form)
                {
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.ShowDialog(owner);
                }
                return true;
            }
            message = formError ?? "الشاشة الأصلية موجودة لكن تعذر إنشاؤها.";
        }

        var entity = ScreenEntityMap.Resolve(access.ScreenName);
        using var fallback = new CatalogDataScreen(_connectionString, access.ScreenName, entity, access);
        fallback.ShowDialog(owner);
        return true;
    }

    private static bool TryResolveItemMaster(string screenName, out string tableName, out string displayName)
    {
        tableName = string.Empty;
        displayName = string.Empty;

        switch (screenName.Trim())
        {
            case "FrmCompany":
                tableName = "Item_Company";
                displayName = "الشركات";
                return true;
            case "FrmClass":
                tableName = "Item_Class";
                displayName = "الفئات";
                return true;
            case "FrmGroups":
                tableName = "Item_Groups";
                displayName = "المجموعات";
                return true;
            default:
                return false;
        }
    }

    private static Type? FindFormType(string screenName)
    {
        if (string.IsNullOrWhiteSpace(screenName))
            return null;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .OrderByDescending(a => a == typeof(ScreenRouter).Assembly))
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            var match = types.FirstOrDefault(t =>
                typeof(Form).IsAssignableFrom(t) &&
                !t.IsAbstract &&
                string.Equals(t.Name, screenName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return match;
        }
        return null;
    }

    private bool TryCreateForm(
        Type type,
        ScreenAccess access,
        out Form? form,
        out string? error)
    {
        form = null;
        error = null;
        try
        {
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                         .OrderBy(c => c.GetParameters().Length))
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length == 0)
                {
                    form = (Form?)ctor.Invoke(null);
                    if (form is not null) return true;
                }
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(AppSession))
                {
                    form = (Form?)ctor.Invoke(new object?[] { _session });
                    if (form is not null) return true;
                }
                if (parameters.Length == 1 &&
                    (parameters[0].ParameterType == typeof(int) || parameters[0].ParameterType == typeof(int?)) &&
                    access.ScreenNum.HasValue)
                {
                    form = (Form?)ctor.Invoke(new object?[] { access.ScreenNum.Value });
                    if (form is not null) return true;
                }
            }
            error = $"الشاشة {type.Name} موجودة لكن لا يوجد Constructor مدعوم حاليًا.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.GetBaseException().Message;
            return false;
        }
    }
}
