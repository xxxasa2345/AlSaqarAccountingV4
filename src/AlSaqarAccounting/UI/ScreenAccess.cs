namespace AlSaqarAccounting.UI;

public sealed class ScreenAccess
{
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
            if (n.Contains("Item", StringComparison.OrdinalIgnoreCase)) return "الأصناف والمخازن";
            if (n.Contains("Account", StringComparison.OrdinalIgnoreCase)) return "الحسابات";
            if (n.Contains("Contract", StringComparison.OrdinalIgnoreCase)) return "العقود";
            if (n.Contains("Payroll", StringComparison.OrdinalIgnoreCase) || n.Contains("Employee", StringComparison.OrdinalIgnoreCase)) return "الموارد البشرية";
            if (n.Contains("Manufacturing", StringComparison.OrdinalIgnoreCase)) return "التصنيع";
            if (n.Contains("Rental", StringComparison.OrdinalIgnoreCase) || n.Contains("Rent", StringComparison.OrdinalIgnoreCase)) return "الإيجارات";
            if (n.Contains("Repair", StringComparison.OrdinalIgnoreCase)) return "الصيانة";
            if (n.Contains("Restaurant", StringComparison.OrdinalIgnoreCase)) return "المطاعم";
            if (n.Contains("Invoice", StringComparison.OrdinalIgnoreCase) || n.Contains("Zatca", StringComparison.OrdinalIgnoreCase)) return "الفوترة الإلكترونية";
            if (n.Contains("Order", StringComparison.OrdinalIgnoreCase) || n.Contains("Purchase", StringComparison.OrdinalIgnoreCase)) return "المبيعات والمشتريات";
            if (n.Contains("Report", StringComparison.OrdinalIgnoreCase)) return "التقارير";
            return "أخرى";
        }
    }
}
