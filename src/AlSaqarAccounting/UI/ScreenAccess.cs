namespace AlSaqarAccounting.UI;

public sealed class ScreenAccess
{
    /// <summary>
    /// Removes display-only prefixes accidentally stored with legacy screen labels.
    /// Examples: "+ الكاشير" -> "الكاشير", "» المبيعات" -> "المبيعات".
    /// The security row itself remains unchanged; this is only the canonical runtime name.
    /// </summary>
    public static string CleanScreenName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var name = value.Trim();
        while (name.Length > 0)
        {
            name = name.TrimStart();
            if (name.StartsWith("+", StringComparison.Ordinal) ||
                name.StartsWith("»", StringComparison.Ordinal) ||
                name.StartsWith(">", StringComparison.Ordinal) ||
                name.StartsWith("•", StringComparison.Ordinal) ||
                name.StartsWith("-", StringComparison.Ordinal))
            {
                name = name.Substring(1).TrimStart();
                continue;
            }
            break;
        }

        return name.Trim();
    }

    public int Id { get; init; }
    public string ScreenName { get; init; } = string.Empty;
    public int? ScreenTypeId { get; init; }
    public int? ScreenNum { get; init; }
    public string ScreenTypeName { get; init; } = string.Empty;
    public bool IsShow { get; init; }
    public bool AllowBranch { get; init; }
    public bool AllowEnter { get; init; }
    public bool AllowSave { get; init; }
    public bool AllowEdit { get; init; }
    public bool AllowDelete { get; init; }
    public bool AllowPrint { get; init; }
    public bool AllowExport { get; init; }

    public string ModuleDisplayName => ModuleNames.ToArabic(ScreenTypeName, ScreenName);

    private static class ModuleNames
    {
        private static readonly Dictionary<string, string> Known =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Account"] = "الحسابات",
                ["CloseYears"] = "إقفال السنوات",
                ["Contract"] = "العقود",
                ["Core"] = "النظام",
                ["Cust_Sup"] = "العملاء والموردون",
                ["ElctorncInvoic"] = "الفوترة الإلكترونية",
                ["FrmOthers"] = "أخرى",
                ["FrmRestaurant"] = "المطاعم",
                ["FrmsHome"] = "الرئيسية",
                ["FrmsMain"] = "النظام والإعدادات",
                ["Security"] = "الأمن والصلاحيات",
                ["Items"] = "الأصناف والمخازن",
                ["OrderDalala"] = "الدلالة والإرسال",
                ["OrderManufacturing"] = "التصنيع",
                ["OrderRent"] = "الإيجارات",
                ["Orders"] = "المبيعات والمشتريات",
                ["Payroll"] = "الموارد البشرية",
                ["Repairs"] = "الصيانة",
                ["Reports"] = "التقارير",
                ["Scaffolds"] = "السقالات",
                ["Virgins"] = "الإذن والفيزات"
            };

        public static string ToArabic(string raw, string screenName)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var value = raw.Trim();
                if (Known.TryGetValue(value, out var arabic))
                    return arabic;

                return value;
            }

            var n = screenName ?? string.Empty;
            if (n.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0) return "الأصناف والمخازن";
            if (n.IndexOf("Account", StringComparison.OrdinalIgnoreCase) >= 0) return "الحسابات";
            if (n.IndexOf("Contract", StringComparison.OrdinalIgnoreCase) >= 0) return "العقود";
            if (n.IndexOf("Payroll", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Employee", StringComparison.OrdinalIgnoreCase) >= 0) return "الموارد البشرية";
            if (n.IndexOf("Manufacturing", StringComparison.OrdinalIgnoreCase) >= 0) return "التصنيع";
            if (n.IndexOf("Rental", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Rent", StringComparison.OrdinalIgnoreCase) >= 0) return "الإيجارات";
            if (n.IndexOf("Repair", StringComparison.OrdinalIgnoreCase) >= 0) return "الصيانة";
            if (n.IndexOf("Restaurant", StringComparison.OrdinalIgnoreCase) >= 0) return "المطاعم";
            if (n.IndexOf("Invoice", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Zatca", StringComparison.OrdinalIgnoreCase) >= 0) return "الفوترة الإلكترونية";
            if (n.IndexOf("Order", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Purchase", StringComparison.OrdinalIgnoreCase) >= 0) return "المبيعات والمشتريات";
            if (n.IndexOf("Report", StringComparison.OrdinalIgnoreCase) >= 0) return "التقارير";
            return "أخرى";
        }
    }
}
