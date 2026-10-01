using System.Reflection;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Services;
using AlSaqarAccounting.Forms;

namespace AlSaqarAccounting.UI;

public sealed class ScreenRouter
{
    private readonly string _connectionString;
    private readonly AppSession _session;

    public ScreenRouter(string connectionString, AppSession session)
    { _connectionString = connectionString; _session = session; }

    public bool TryOpen(Form owner, ScreenAccess access, out string message)
    {
        message = string.Empty;
        if (!access.AllowEnter) { message = "لا تملك صلاحية فتح هذه الشاشة."; return false; }
        var db = new DbExecutor(new SqlConnectionFactory(_connectionString));

        if (string.Equals(access.ScreenName, "إدارة التراخيص", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(access.ScreenName, "التراخيص", StringComparison.OrdinalIgnoreCase))
        {
            if (_session.GroupId != 1) { message = "إدارة التراخيص متاحة للمجموعة الإدارية فقط."; return false; }
            using var form = new LicenseManagementForm(_session, new LicenseService(db)) { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }

        if (IsPasswordScreen(access.ScreenName))
        {
            using var form = new ChangePasswordForm(_session, new SecurityAdministrationService(db))
            { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }

        if (IsScreenCatalogScreen(access.ScreenName))
        {
            if (!access.AllowEnter) { message = "لا تملك صلاحية فتح شاشة إدارة شاشات النظام."; return false; }
            using var form = new UserScreensForm(_session, access, new SecurityAdministrationService(db))
            { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }

        if (IsPermissionScreen(access.ScreenName))
        {
            if (!access.AllowEnter) { message = "لا تملك صلاحية فتح شاشة إدارة الصلاحيات."; return false; }
            using var form = new UserPermissionsForm(_session, access, new SecurityAdministrationService(db))
            { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }

        // Original GTSErpSystem places user-management screens in the Security
        // module. Keep those separate from item groups.
        if (IsUserScreen(access.ScreenName))
        {
            if (!access.AllowEnter) { message = "لا تملك صلاحية فتح إدارة المستخدمين."; return false; }
            using var form = new UserManagementForm(_session, new UserManagementService(db)) { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }

        if (IsSecurityGroupPermissionScreen(access.ScreenName))
        {
            if (_session.GroupId != 1) { message = "إدارة صلاحيات المجموعة متاحة للمجموعة الإدارية فقط."; return false; }
            using var form = new UserPermissionsForm(_session, access, new SecurityAdministrationService(db))
            { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }

        if (IsUserGroupScreen(access.ScreenName))
        {
            if (!access.AllowEnter) { message = "لا تملك صلاحية فتح مجموعات المستخدمين."; return false; }
            using var form = new UserGroupsForm(_session, new UserGroupsService(db)) { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }

        if (string.Equals(access.ScreenName, "FrmUnit", StringComparison.OrdinalIgnoreCase))
        {
            using var form = new ItemUnitForm(_session, access, new ItemUnitService(db)) { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }
        if (TryResolveItemMaster(access.ScreenName, out var tableName, out var displayName))
        {
            using var form = new ItemMasterForm(_session, access, new ItemMasterService(db), tableName, displayName) { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }
        if (string.Equals(access.ScreenName, "FrmItems", StringComparison.OrdinalIgnoreCase))
        {
            using var form = new ItemsForm(_session, access, new ItemsService(db)) { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }
        if (string.Equals(access.ScreenName, "InvoicesForm", StringComparison.OrdinalIgnoreCase) || string.Equals(access.ScreenName, "الفواتير", StringComparison.OrdinalIgnoreCase))
        {
            using var form = new InvoicesForm(_session, access, new InvoiceService(db), new SalesService(db), new StoresService(db), new CustomerService(db), new SupplierService(db), new ItemsService(db)) { StartPosition = FormStartPosition.CenterParent };
            form.ShowDialog(owner); return true;
        }
        if (RealScreenCatalog.TryCreate(access.ScreenName, _connectionString, _session, access, out var realScreen) && realScreen is not null)
        {
            using (realScreen) { realScreen.StartPosition = FormStartPosition.CenterParent; realScreen.ShowDialog(owner); }
            return true;
        }
        var type = FindFormType(access.ScreenName);
        if (type is not null && type != typeof(DynamicErpScreenForm))
        {
            if (TryCreateForm(type, access, out var form, out var formError) && form is not null)
            { using (form) { form.StartPosition = FormStartPosition.CenterParent; form.ShowDialog(owner); } return true; }
            message = formError ?? "الشاشة الأصلية موجودة لكن تعذر إنشاؤها.";
        }
        try
        {
            using var dynamicScreen = new DynamicErpScreenForm(_session, access, new DynamicErpScreenService(db), access.ScreenName) { StartPosition = FormStartPosition.CenterParent };
            dynamicScreen.ShowDialog(owner); return true;
        }
        catch (Exception ex) { message = ex.GetBaseException().Message; return false; }
    }

    private static bool IsPasswordScreen(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim();
        return n.Equals("FrmPassword", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("كلمة المرور", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("تغيير كلمة المرور", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("ChangePasswordForm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScreenCatalogScreen(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim();
        return n.Equals("FrmScreens", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("الشاشات", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("شاشات النظام", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("UserScreensForm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPermissionScreen(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim();
        return n.Equals("FrmPermission", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("FrmPermissions", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("الصلاحيات", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("صلاحيات الشاشات", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("UserPermissionsForm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUserScreen(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim();
        return n.Equals("مستخدم جديد", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("إضافة مستخدم", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("تعديل مستخدم", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("المستخدمون", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("المستخدمين", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("المستخدمون النظام", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("FrmUsers", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("FrmEditUser", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("UserManagementForm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSecurityGroupPermissionScreen(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim();
        return n.Equals("FrmSecurityGroup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUserGroupScreen(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim();
        return n.Equals("FrmGroups", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("مجموعات المستخدمين", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("مجموعة المستخدمين", StringComparison.OrdinalIgnoreCase) ||
               n.Equals("UserGroupsForm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveItemMaster(string screenName, out string tableName, out string displayName)
    {
        tableName = string.Empty; displayName = string.Empty;
        switch (screenName.Trim())
        {
            case "FrmCompany": tableName = "Item_Company"; displayName = "الشركات"; return true;
            case "FrmClass": tableName = "Item_Class"; displayName = "الفئات"; return true;
            default: return false;
        }
    }

    private static Type? FindFormType(string screenName)
    {
        if (string.IsNullOrWhiteSpace(screenName)) return null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderByDescending(a => a == typeof(ScreenRouter).Assembly))
        {
            Type[] types;
            try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }
            var match = types.FirstOrDefault(t => typeof(Form).IsAssignableFrom(t) && !t.IsAbstract && string.Equals(t.Name, screenName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return null;
    }

    private bool TryCreateForm(Type type, ScreenAccess access, out Form? form, out string? error)
    {
        form = null; error = null;
        try
        {
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderBy(c => c.GetParameters().Length))
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length == 0) { form = (Form?)ctor.Invoke(null); if (form is not null) return true; }
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(AppSession)) { form = (Form?)ctor.Invoke(new object?[] { _session }); if (form is not null) return true; }
                if (parameters.Length == 1 && (parameters[0].ParameterType == typeof(int) || parameters[0].ParameterType == typeof(int?)) && access.ScreenNum.HasValue) { form = (Form?)ctor.Invoke(new object?[] { access.ScreenNum.Value }); if (form is not null) return true; }
            }
            error = $"الشاشة {type.Name} موجودة لكن لا يوجد Constructor مدعوم حاليًا."; return false;
        }
        catch (Exception ex) { error = ex.GetBaseException().Message; return false; }
    }
}
