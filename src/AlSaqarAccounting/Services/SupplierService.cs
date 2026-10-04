using System.Data;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Supplier management (Account_CustSup where IsSuppliers = true).
/// </summary>
public sealed class SupplierService
{
    private readonly DbExecutor _db;

    public SupplierService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var sql = branchId.HasValue
            ? "SELECT * FROM dbo.Account_CustSup WHERE IsSuppliers = 1 AND (BranchID = @BranchID OR BranchID IS NULL) ORDER BY CustSuppName"
            : "SELECT * FROM dbo.Account_CustSup WHERE IsSuppliers = 1 ORDER BY CustSuppName";
        
        return branchId.HasValue
            ? _db.QueryAsync(sql, p => p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value, cancellationToken)
            : _db.QueryAsync(sql, cancellationToken: cancellationToken);
    }

    public Task<DataTable> SearchAsync(string searchTerm, int? branchId = null, CancellationToken cancellationToken = default)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
            return ListAsync(branchId, cancellationToken);

        var sql = branchId.HasValue
            ? @"SELECT * FROM dbo.Account_CustSup 
               WHERE IsSuppliers = 1 
                 AND (BranchID = @BranchID OR BranchID IS NULL)
                 AND (CustSuppName LIKE '%' + @Search + '%' 
                      OR CustSuppCode LIKE '%' + @Search + '%' 
                      OR Phone LIKE '%' + @Search + '%' 
                      OR VatNum LIKE '%' + @Search + '%')
               ORDER BY CustSuppName"
            : @"SELECT * FROM dbo.Account_CustSup 
               WHERE IsSuppliers = 1 
                 AND (CustSuppName LIKE '%' + @Search + '%' 
                      OR CustSuppCode LIKE '%' + @Search + '%' 
                      OR Phone LIKE '%' + @Search + '%' 
                      OR VatNum LIKE '%' + @Search + '%')
               ORDER BY CustSuppName";

        return _db.QueryAsync(sql, p =>
        {
            p.Add("@Search", SqlDbType.NVarChar, 100).Value = term;
            if (branchId.HasValue)
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
        }, cancellationToken);
    }

    public async Task<int> CreateAsync(Account_CustSup supplier, AppSession session, CancellationToken cancellationToken = default)
    {
        Validate(supplier);
        supplier.IsSuppliers = true;
        supplier.IsCustomers = false;

        const string sql = @"
INSERT INTO dbo.Account_CustSup (
    CustSuppCode, AccountNo, BranchID, CustSuppName, VatNum, Phone, Fax, Address,
    PlaceID, SalesManID, CreditLimit, AlarmLimit, IsSuppliers, IsCustomers, FrmCust,
    Note, UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add,
    BuildingNum, Street, District, City, Country, PostalCode, AdditionalNum, CommercialRecord
)
VALUES (
    @CustSuppCode, @AccountNo, @BranchID, @CustSuppName, @VatNum, @Phone, @Fax, @Address,
    @PlaceID, @SalesManID, @CreditLimit, @AlarmLimit, @IsSuppliers, @IsCustomers, @FrmCust,
    @Note, @UserID_Add, @UserBranch_Add, @UserMacAddress_Add, GETDATE(),
    @BuildingNum, @Street, @District, @City, @Country, @PostalCode, @AdditionalNum, @CommercialRecord
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var result = await _db.QueryAsync(sql, p =>
        {
            p.Add("@CustSuppCode", SqlDbType.Int).Value = (object?)supplier.CustSuppCode ?? DBNull.Value;
            p.Add("@AccountNo", SqlDbType.Int).Value = (object?)supplier.AccountNo ?? DBNull.Value;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)supplier.BranchID ?? DBNull.Value;
            p.Add("@CustSuppName", SqlDbType.NVarChar, 300).Value = supplier.CustSuppName.Trim();
            p.Add("@VatNum", SqlDbType.NVarChar, 100).Value = (object?)supplier.VatNum ?? DBNull.Value;
            p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)supplier.Phone ?? DBNull.Value;
            p.Add("@Fax", SqlDbType.NVarChar, 100).Value = (object?)supplier.Fax ?? DBNull.Value;
            p.Add("@Address", SqlDbType.NVarChar, 500).Value = (object?)supplier.Address ?? DBNull.Value;
            p.Add("@PlaceID", SqlDbType.Int).Value = (object?)supplier.PlaceID ?? DBNull.Value;
            p.Add("@SalesManID", SqlDbType.Int).Value = (object?)supplier.SalesManID ?? DBNull.Value;
            p.Add("@CreditLimit", SqlDbType.Decimal).Value = (object?)supplier.CreditLimit ?? DBNull.Value;
            p.Add("@AlarmLimit", SqlDbType.Decimal).Value = (object?)supplier.AlarmLimit ?? DBNull.Value;
            p.Add("@IsSuppliers", SqlDbType.Bit).Value = supplier.IsSuppliers;
            p.Add("@IsCustomers", SqlDbType.Bit).Value = supplier.IsCustomers;
            p.Add("@FrmCust", SqlDbType.Bit).Value = supplier.FrmCust;
            p.Add("@Note", SqlDbType.NVarChar, 1000).Value = (object?)supplier.Note ?? DBNull.Value;
            p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Add", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = GetMachineMac();
            p.Add("@BuildingNum", SqlDbType.NVarChar, 100).Value = (object?)supplier.BuildingNum ?? DBNull.Value;
            p.Add("@Street", SqlDbType.NVarChar, 200).Value = (object?)supplier.Street ?? DBNull.Value;
            p.Add("@District", SqlDbType.NVarChar, 200).Value = (object?)supplier.District ?? DBNull.Value;
            p.Add("@City", SqlDbType.NVarChar, 100).Value = (object?)supplier.City ?? DBNull.Value;
            p.Add("@Country", SqlDbType.NVarChar, 100).Value = (object?)supplier.Country ?? DBNull.Value;
            p.Add("@PostalCode", SqlDbType.NVarChar, 50).Value = (object?)supplier.PostalCode ?? DBNull.Value;
            p.Add("@AdditionalNum", SqlDbType.NVarChar, 100).Value = (object?)supplier.AdditionalNum ?? DBNull.Value;
            p.Add("@CommercialRecord", SqlDbType.NVarChar, 100).Value = (object?)supplier.CommercialRecord ?? DBNull.Value;
        }, cancellationToken);

        if (result.Rows.Count == 0)
            throw new InvalidOperationException("لم يتم إنشاء المورد"); // "لم يتم إنشاء المورد"

        return Convert.ToInt32(result.Rows[0][0]);
    }

    public async Task<int> UpdateAsync(Account_CustSup supplier, AppSession session, CancellationToken cancellationToken = default)
    {
        Validate(supplier);
        supplier.IsSuppliers = true;

        const string sql = @"
UPDATE dbo.Account_CustSup
SET 
    CustSuppCode = @CustSuppCode,
    AccountNo = @AccountNo,
    BranchID = @BranchID,
    CustSuppName = @CustSuppName,
    VatNum = @VatNum,
    Phone = @Phone,
    Fax = @Fax,
    Address = @Address,
    PlaceID = @PlaceID,
    SalesManID = @SalesManID,
    CreditLimit = @CreditLimit,
    AlarmLimit = @AlarmLimit,
    IsSuppliers = @IsSuppliers,
    IsCustomers = @IsCustomers,
    FrmCust = @FrmCust,
    Note = @Note,
    UserID_Update = @UserID_Update,
    UserBranch_Update = @UserBranch_Update,
    UserMacAddress_Update = @UserMacAddress_Update,
    UserDate_Update = GETDATE(),
    BuildingNum = @BuildingNum,
    Street = @Street,
    District = @District,
    City = @City,
    Country = @Country,
    PostalCode = @PostalCode,
    AdditionalNum = @AdditionalNum,
    CommercialRecord = @CommercialRecord
WHERE ID = @ID;";

        return await _db.ExecuteAsync(sql, p =>
        {
            p.Add("@ID", SqlDbType.Int).Value = supplier.ID;
            p.Add("@CustSuppCode", SqlDbType.Int).Value = (object?)supplier.CustSuppCode ?? DBNull.Value;
            p.Add("@AccountNo", SqlDbType.Int).Value = (object?)supplier.AccountNo ?? DBNull.Value;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)supplier.BranchID ?? DBNull.Value;
            p.Add("@CustSuppName", SqlDbType.NVarChar, 300).Value = supplier.CustSuppName.Trim();
            p.Add("@VatNum", SqlDbType.NVarChar, 100).Value = (object?)supplier.VatNum ?? DBNull.Value;
            p.Add("@Phone", SqlDbType.NVarChar, 100).Value = (object?)supplier.Phone ?? DBNull.Value;
            p.Add("@Fax", SqlDbType.NVarChar, 100).Value = (object?)supplier.Fax ?? DBNull.Value;
            p.Add("@Address", SqlDbType.NVarChar, 500).Value = (object?)supplier.Address ?? DBNull.Value;
            p.Add("@PlaceID", SqlDbType.Int).Value = (object?)supplier.PlaceID ?? DBNull.Value;
            p.Add("@SalesManID", SqlDbType.Int).Value = (object?)supplier.SalesManID ?? DBNull.Value;
            p.Add("@CreditLimit", SqlDbType.Decimal).Value = (object?)supplier.CreditLimit ?? DBNull.Value;
            p.Add("@AlarmLimit", SqlDbType.Decimal).Value = (object?)supplier.AlarmLimit ?? DBNull.Value;
            p.Add("@IsSuppliers", SqlDbType.Bit).Value = supplier.IsSuppliers;
            p.Add("@IsCustomers", SqlDbType.Bit).Value = supplier.IsCustomers;
            p.Add("@FrmCust", SqlDbType.Bit).Value = supplier.FrmCust;
            p.Add("@Note", SqlDbType.NVarChar, 1000).Value = (object?)supplier.Note ?? DBNull.Value;
            p.Add("@UserID_Update", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Update", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Update", SqlDbType.NVarChar, 200).Value = GetMachineMac();
            p.Add("@BuildingNum", SqlDbType.NVarChar, 100).Value = (object?)supplier.BuildingNum ?? DBNull.Value;
            p.Add("@Street", SqlDbType.NVarChar, 200).Value = (object?)supplier.Street ?? DBNull.Value;
            p.Add("@District", SqlDbType.NVarChar, 200).Value = (object?)supplier.District ?? DBNull.Value;
            p.Add("@City", SqlDbType.NVarChar, 100).Value = (object?)supplier.City ?? DBNull.Value;
            p.Add("@Country", SqlDbType.NVarChar, 100).Value = (object?)supplier.Country ?? DBNull.Value;
            p.Add("@PostalCode", SqlDbType.NVarChar, 50).Value = (object?)supplier.PostalCode ?? DBNull.Value;
            p.Add("@AdditionalNum", SqlDbType.NVarChar, 100).Value = (object?)supplier.AdditionalNum ?? DBNull.Value;
            p.Add("@CommercialRecord", SqlDbType.NVarChar, 100).Value = (object?)supplier.CommercialRecord ?? DBNull.Value;
        }, cancellationToken);
    }

    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync("DELETE FROM dbo.Account_CustSup WHERE ID = @ID AND IsSuppliers = 1;",
            p => p.Add("@ID", SqlDbType.Int).Value = id, cancellationToken);

    public Task<Account_CustSup?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.QuerySingleAsync<Account_CustSup>(
            "SELECT * FROM dbo.Account_CustSup WHERE ID = @ID AND IsSuppliers = 1;",
            p => p.Add("@ID", SqlDbType.Int).Value = id, cancellationToken);

    private static void Validate(Account_CustSup supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier.CustSuppName))
            throw new ArgumentException("اسم المورد مطلوب"); // "اسم المورد مطلوب"
        if (supplier.CustSuppName.Trim().Length > 300)
            throw new ArgumentException("اسم المورد طويل جدا"); // "اسم المورد طويل جدا"
    }

    private static string GetMachineMac()
    {
        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Select(n => n.GetPhysicalAddress()?.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return string.IsNullOrWhiteSpace(mac) ? Environment.MachineName : mac;
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
