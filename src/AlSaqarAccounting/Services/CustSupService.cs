using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for customers and suppliers.
/// Customers are read through the original Select_AccountCustomer procedure.
/// Suppliers live in Account_CustSup with IsSuppliers = 1 and are maintained
/// with fully parameterized statements whose columns come from the forensic
/// schema export only.
/// </summary>
public sealed class CustSupService
{
    private readonly DbExecutor _db;

    public CustSupService(DbExecutor db) => _db = db;

    public Task<DataTable> ListCustomersAsync(int? branchId, CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.Select_AccountCustomer",
            p =>
            {
                if (!branchId.HasValue)
                    throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            },
            cancellationToken);

    public Task<DataTable> ListSuppliersAsync(CancellationToken cancellationToken = default)
        => _db.QueryAsync(
            @"
SELECT ID, CustSuppName, VatNum, Phone, Fax, Address,
       BuildingNum, Street, District, City, Country, PostalCode, AdditionalNum,
       CommercialRecord, CreditLimit, Note,
       UserBranch_Add, UserDate_Add, UserDate_Update
FROM dbo.Account_CustSup
WHERE IsSuppliers = 1
ORDER BY CustSuppName, ID;",
            cancellationToken: cancellationToken);

    public Task<int> CreateSupplierAsync(
        string name,
        string? vatNumber,
        string? phone,
        string? address,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return InsertSupplierAsync(name, vatNumber, phone, address, session, insertId: true, cancellationToken);
    }

    public Task<int> UpdateSupplierAsync(
        int id,
        string name,
        string? vatNumber,
        string? phone,
        string? address,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        return _db.ExecuteAsync(
            @"
UPDATE dbo.Account_CustSup
SET CustSuppName = @Name,
    VatNum = @Vat,
    Phone = @Phone,
    Address = @Address,
    UserID_Update = @UserID,
    UserBranch_Update = @BranchID,
    UserMacAddress_Update = @Mac,
    UserDate_Update = GETDATE()
WHERE ID = @ID AND IsSuppliers = 1;",
            p =>
            {
                p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
                p.Add("@Vat", SqlDbType.NVarChar, 100).Value = (object?)vatNumber?.Trim() ?? DBNull.Value;
                p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)phone?.Trim() ?? DBNull.Value;
                p.Add("@Address", SqlDbType.NVarChar, 300).Value = (object?)address?.Trim() ?? DBNull.Value;
                p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                p.Add("@ID", SqlDbType.Int).Value = id;
            },
            cancellationToken);
    }

    public Task<int> DeleteSupplierAsync(int id, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync(
            "DELETE FROM dbo.Account_CustSup WHERE ID = @ID AND IsSuppliers = 1;",
            p => p.Add("@ID", SqlDbType.Int).Value = id,
            cancellationToken);

    /// <summary>
    /// The ID column is an identity in most GTSdb2026 exports. If the server
    /// rejects the identity-style insert because ID is not identity (error 515),
    /// the row is inserted again with an explicitly computed ID.
    /// </summary>
    private async Task<int> InsertSupplierAsync(
        string name,
        string? vatNumber,
        string? phone,
        string? address,
        AppSession session,
        bool insertId,
        CancellationToken cancellationToken)
    {
        const string identitySql = @"
INSERT INTO dbo.Account_CustSup
    (CustSuppName, VatNum, Phone, Address, IsSuppliers, IsCustomers, BranchID,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
VALUES
    (@Name, @Vat, @Phone, @Address, 1, 0, @BranchID,
     @UserID, @BranchID, @Mac, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS int);";

        const string computedIdSql = @"
INSERT INTO dbo.Account_CustSup
    (ID, CustSuppName, VatNum, Phone, Address, IsSuppliers, IsCustomers, BranchID,
     UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add)
SELECT
    (SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.Account_CustSup),
    @Name, @Vat, @Phone, @Address, 1, 0, @BranchID,
    @UserID, @BranchID, @Mac, GETDATE();
SELECT (SELECT ISNULL(MAX(ID), 0) FROM dbo.Account_CustSup);";

        try
        {
            var result = await _db.QueryAsync(
                insertId ? identitySql : computedIdSql,
                BuildInsertParameters(name, vatNumber, phone, address, session),
                cancellationToken);
            return Convert.ToInt32(result.Rows[0][0]);
        }
        catch (SqlException ex) when (insertId && IsMissingValueError(ex))
        {
            var result = await _db.QueryAsync(
                computedIdSql,
                BuildInsertParameters(name, vatNumber, phone, address, session),
                cancellationToken);
            return Convert.ToInt32(result.Rows[0][0]);
        }
    }

    private static Action<SqlParameterCollection> BuildInsertParameters(
        string name,
        string? vatNumber,
        string? phone,
        string? address,
        AppSession session)
    {
        return p =>
        {
            p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
            p.Add("@Vat", SqlDbType.NVarChar, 100).Value = (object?)vatNumber?.Trim() ?? DBNull.Value;
            p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)phone?.Trim() ?? DBNull.Value;
            p.Add("@Address", SqlDbType.NVarChar, 300).Value = (object?)address?.Trim() ?? DBNull.Value;
            p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
        };
    }

    private static bool IsMissingValueError(SqlException ex)
        => ex.Number == 515; // Cannot insert the value NULL into a non-identity required column.

    // ---------------------------------------------------------------
    // العملاء — إضافة عميل حقيقي: حساب فرعي تحت الحساب الافتراضي
    // للعملاء + سجل في Account_DefualtCustomer، تماماً كما في النظام الأصلي.
    // ---------------------------------------------------------------

    /// <summary>Registers a new customer row in Account_DefualtCustomer,
    /// bound to the GL account created for it under the default-customer
    /// parent (handled by AccountsTreeService).</summary>
    public async Task<int> RegisterCustomerAsync(
        string name,
        int accountId,
        string? phone,
        string? vatNumber,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        ValidateCustomerName(name);
        if (accountId <= 0)
            throw new ArgumentException("رقم حساب العميل غير صحيح.");

        return await InsertCustomerRowAsync(
            name, accountId, phone, vatNumber, session, insertSn: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Deletes a customer registration (the GL account is left to
    /// the chart-of-accounts screen).</summary>
    public Task<int> DeleteCustomerAsync(int sn, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync(
            "DELETE FROM dbo.Account_DefualtCustomer WHERE SN = @SN;",
            p => p.Add("@SN", SqlDbType.Int).Value = sn,
            cancellationToken);

    private async Task<int> InsertCustomerRowAsync(
        string name,
        int accountId,
        string? phone,
        string? vatNumber,
        AppSession session,
        bool insertSn,
        CancellationToken cancellationToken)
    {
        const string identitySql = @"
INSERT INTO dbo.Account_DefualtCustomer
    (ID, Name, AccountID, BranchID)
SELECT
    (SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.Account_DefualtCustomer),
    @Name, @AccountID, @BranchID;
SELECT ISNULL(MAX(SN), 0) FROM dbo.Account_DefualtCustomer;";

        const string computedSnSql = @"
INSERT INTO dbo.Account_DefualtCustomer
    (SN, ID, Name, AccountID, BranchID)
SELECT
    (SELECT ISNULL(MAX(SN), 0) + 1 FROM dbo.Account_DefualtCustomer),
    (SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.Account_DefualtCustomer),
    @Name, @AccountID, @BranchID;
SELECT ISNULL(MAX(SN), 0) FROM dbo.Account_DefualtCustomer;";

        try
        {
            var result = await _db.QueryAsync(
                insertSn ? identitySql : computedSnSql,
                p => BuildCustomerParameters(name, accountId, phone, vatNumber, session),
                cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result.Rows[0][0]);
        }
        catch (SqlException ex) when (insertSn && ex.Number == 515)
        {
            var result = await _db.QueryAsync(
                computedSnSql,
                p => BuildCustomerParameters(name, accountId, phone, vatNumber, session),
                cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result.Rows[0][0]);
        }
    }

    private static Action<SqlParameterCollection> BuildCustomerParameters(
        string name,
        int accountId,
        string? phone,
        string? vatNumber,
        AppSession session)
    {
        return p =>
        {
            p.Add("@Name", SqlDbType.NVarChar, 300).Value = name.Trim();
            p.Add("@AccountID", SqlDbType.Int).Value = accountId;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
        };
    }

    private static void ValidateCustomerName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 300)
            throw new ArgumentException("اسم العميل يجب أن يكون بين 1 و300 حرف.");
    }

    private static void ValidateName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 300)
            throw new ArgumentException("اسم المورد يجب أن يكون بين 1 و300 حرف.");
    }
}

