using System.Reflection;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.UI;

/// <summary>
/// Opens an extracted/original WinForms screen when its Form type exists.
/// When the original Form implementation is not yet present in V4, it opens a
/// read-only DB-backed screen using the same security record and entity hint.
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

        if (OperationalScreenRegistry.TryResolve(access.ScreenName, out var operational))
        {
            var mappedType = operational.TargetFormName is null
                ? null
                : FindFormType(operational.TargetFormName);

            if (mappedType is not null &&
                TryCreateForm(mappedType, access, out var mappedForm, out var mappedError) &&
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

            message = formError ?? "تعذر إنشاء الشاشة الأصلية.";
        }

        var entity = ScreenEntityMap.Resolve(access.ScreenName);
        using var fallback = new CatalogDataScreen(_connectionString, access.ScreenName, entity, access);
        fallback.ShowDialog(owner);

        return true;
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

                if (parameters.Length == 1 &&
                    parameters[0].ParameterType == typeof(AppSession))
                {
                    form = (Form?)ctor.Invoke(new object?[] { _session });
                    if (form is not null) return true;
                }

                if (parameters.Length == 1 &&
                    (parameters[0].ParameterType == typeof(int) ||
                     parameters[0].ParameterType == typeof(int?)) &&
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
