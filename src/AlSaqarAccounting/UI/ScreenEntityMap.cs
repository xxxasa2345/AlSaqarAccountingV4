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
        if (n.IndexOf("أصناف", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("الصنف", StringComparison.OrdinalIgnoreCase) >= 0) return "Item_Items";
        if (n.IndexOf("عميل", StringComparison.OrdinalIgnoreCase) >= 0) return "Account_DefualtCustomer";
        if (n.IndexOf("مورد", StringComparison.OrdinalIgnoreCase) >= 0) return "Account_CustSup";
        if (n.IndexOf("مخزن", StringComparison.OrdinalIgnoreCase) >= 0) return "Account_Stores";
        if (n.IndexOf("حساب", StringComparison.OrdinalIgnoreCase) >= 0) return "Account_Accounts";
        if (n.IndexOf("وحدة", StringComparison.OrdinalIgnoreCase) >= 0) return "Item_Unit";
        if (n.IndexOf("موظف", StringComparison.OrdinalIgnoreCase) >= 0) return "Emp_Employee";
        if (n.IndexOf("رواتب", StringComparison.OrdinalIgnoreCase) >= 0) return "Emp_Payroll";
        if (n.IndexOf("عقد", StringComparison.OrdinalIgnoreCase) >= 0) return "Contract_Contract";
        if (n.IndexOf("ضمان", StringComparison.OrdinalIgnoreCase) >= 0) return "Contract_Guarantee";
        return null;
    }
}
