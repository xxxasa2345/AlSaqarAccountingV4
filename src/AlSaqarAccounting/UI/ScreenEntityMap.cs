namespace AlSaqarAccounting.UI;

public static class ScreenEntityMap
{
    private static readonly Dictionary<string, string> Exact =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FrmAccountTree"] = "Account_Accounts",
            ["FrmCardAccount"] = "Account_Accounts",
            ["FrmCostCenter"] = "Account_CostCenters",
            ["FrmCostCenterTree"] = "Account_CostCenters",
            ["FrmDefualtAccount"] = "Account_DefualtAccount",
            ["FrmDefualtCustomer"] = "Account_DefualtCustomer",
            ["FrmPayment"] = "Order_PaymentItem",
            ["FrmReceipts"] = "Account_Receipts",
            ["FrmProjects"] = "Account_Projects",
            ["FrmBranches"] = "Account_Branch",
            ["FrmCustomer"] = "Account_DefualtCustomer",
            ["FrmSuppliers"] = "Account_CustSup",
            ["FrmStores"] = "Account_Stores",
            ["FrmSalesMan"] = "Account_SalesMan",
            ["FrmPlace"] = "Account_Place",
            ["FrmReservation"] = "Order_Reservation",
            ["FrmItems"] = "Item_Items",
            ["FrmUnit"] = "Item_Unit",
            ["FrmClass"] = "Item_Class",
            ["FrmCompany"] = "Item_Company",
            ["FrmCountry"] = "Item_Country",
            ["FrmDoctor"] = "Item_Doctor",
            ["FrmGuarantee"] = "Contract_Guarantee",
            ["FrmDalala"] = "OrderDalala_Dalala",
            ["FrmSending"] = "OrderDalala_Sending",
            ["FrmManufacturing"] = "Order_Manufacturing",
            ["FrmManufacturingOrder"] = "Order_ManufacturingOrder",
            ["FrmRent"] = "OrderRent_Rent",
            ["FrmRecipt"] = "OrderRent_Recipt",
            ["FrmRecipt3"] = "OrderRent_Recipt3",
            ["FrmRentalExpenses"] = "RentalExpenses",
            ["FrmRentalInvoice"] = "Orders_RentalInvoice",
            ["FrmContract"] = "Contract_Contract",
            ["FrmEJAR"] = "Contract_EJAR",
            ["FrmEntryPermission"] = "Scaffold_EntryPermission",
            ["FrmExitPermission"] = "Scaffold_ExitPermission",
            ["FrmEmployee"] = "Emp_Employee",
            ["FrmPayroll"] = "Emp_Payroll",
            ["FrmRepairs"] = "Repairs_Repair",
            ["FrmRepair"] = "Repairs_Repair",
            ["FrmDelivery"] = "Restaurant_Delivery",
            ["FrmRoom"] = "Restaurant_Room",
            ["FrmTable"] = "Restaurant_Table",
            ["FrmZatcaIntgration"] = "ResultElectronicInvoiceXmls"
        };

    public static string? Resolve(string screenName)
    {
        if (string.IsNullOrWhiteSpace(screenName))
            return null;

        if (Exact.TryGetValue(screenName.Trim(), out var table))
            return table;

        var n = screenName.Trim();
        if (n.Contains("أصناف", StringComparison.OrdinalIgnoreCase) || n.Contains("الصنف", StringComparison.OrdinalIgnoreCase)) return "Item_Items";
        if (n.Contains("عميل", StringComparison.OrdinalIgnoreCase)) return "Account_DefualtCustomer";
        if (n.Contains("مورد", StringComparison.OrdinalIgnoreCase)) return "Account_CustSup";
        if (n.Contains("مخزن", StringComparison.OrdinalIgnoreCase)) return "Account_Stores";
        if (n.Contains("حساب", StringComparison.OrdinalIgnoreCase)) return "Account_Accounts";
        if (n.Contains("وحدة", StringComparison.OrdinalIgnoreCase)) return "Item_Unit";
        if (n.Contains("موظف", StringComparison.OrdinalIgnoreCase)) return "Emp_Employee";
        if (n.Contains("رواتب", StringComparison.OrdinalIgnoreCase)) return "Emp_Payroll";
        if (n.Contains("عقد", StringComparison.OrdinalIgnoreCase)) return "Contract_Contract";
        if (n.Contains("ضمان", StringComparison.OrdinalIgnoreCase)) return "Contract_Guarantee";
        return null;
    }
}
