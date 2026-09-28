namespace AlSaqarAccounting.UI;

public sealed record OperationalScreenDefinition(
    string MatchName,
    string DisplayName,
    string? TargetFormName,
    string? TableName,
    string? StoredProcedure,
    bool PassBranchId,
    string Module,
    string? KeyColumn = null);

public static class OperationalScreenRegistry
{
    private static readonly IReadOnlyList<OperationalScreenDefinition> Definitions =
        new[]
        {
            new OperationalScreenDefinition("الشركات", "الشركات", "FrmCompany", "Item_Company", null, false, "الأصناف والمخازن", "ID"),
            new OperationalScreenDefinition("الوحدات", "الوحدات", "FrmUnit", "Item_Unit", null, false, "الأصناف والمخازن", "ID"),
            new OperationalScreenDefinition("الفئات", "الفئات", "FrmClass", "Item_Class", null, false, "الأصناف والمخازن", "ID"),
            new OperationalScreenDefinition("المجموعات", "المجموعات", "FrmGroups", "Item_Groups", null, false, "الأصناف والمخازن", "ID"),
            new OperationalScreenDefinition("الأصناف", "الأصناف", "FrmItems", null, "Get_All_Items", false, "الأصناف والمخازن", "ItemId"),
            new OperationalScreenDefinition("شجرة الحسابات", "شجرة الحسابات", "FrmAccountTree", null, "Get_Account_Tree", false, "الحسابات", "ID"),
            new OperationalScreenDefinition("الحسابات", "الحسابات", "FrmAccountTree", null, "Get_Account_Tree", false, "الحسابات", "ID"),
            new OperationalScreenDefinition("العملاء", "العملاء", "FrmCustomer", null, "Select_AccountCustomer", true, "العملاء والموردون", "ID"),
            new OperationalScreenDefinition("الموردين", "الموردون", "FrmSuppliers", "Account_CustSup", null, false, "العملاء والموردون", "ID"),
            new OperationalScreenDefinition("الموردون", "الموردون", "FrmSuppliers", "Account_CustSup", null, false, "العملاء والموردون", "ID"),
            new OperationalScreenDefinition("الفروع", "الفروع", "FrmBranches", "Account_Branch", null, false, "العملاء والموردون", "ID"),
            new OperationalScreenDefinition("المبيعات", "المبيعات", "FrmOrders", null, "Select_Order_Orders", true, "المبيعات والمشتريات", "ID"),
            new OperationalScreenDefinition("المشتريات", "المشتريات", "FrmPurchases", null, "Select_Order_Purchases", true, "المبيعات والمشتريات", "ID")
        };

    public static IReadOnlyList<OperationalScreenDefinition> All => Definitions;

    public static bool TryResolve(string screenName, out OperationalScreenDefinition definition)
    {
        definition = null!;

        if (string.IsNullOrWhiteSpace(screenName))
            return false;

        var value = screenName.Trim();

        // Exact names first.
        var exact = Definitions.FirstOrDefault(d =>
            string.Equals(value, d.MatchName, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            definition = exact;
            return true;
        }

        // Avoid stealing reports/search/print screens whose names merely mention a module.
        if (value.IndexOf("بحث", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("تقرير", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("طباعة", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("مصمم", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        foreach (var d in Definitions
                     .OrderByDescending(x => x.MatchName.Length))
        {
            if (value.IndexOf(d.MatchName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                definition = d;
                return true;
            }
        }

        return false;
    }
}
