using System.Data;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real business service for Contract management (Contract_Contract).
/// </summary>
public sealed class ContractService
{
    private readonly DbExecutor _db;

    public ContractService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var sql = branchId.HasValue
            ? "SELECT * FROM dbo.Contract_Contract WHERE (BranchID = @BranchID OR BranchID IS NULL) ORDER BY Purchases_Date DESC"
            : "SELECT * FROM dbo.Contract_Contract ORDER BY Purchases_Date DESC";
        
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
            ? @"SELECT * FROM dbo.Contract_Contract 
               WHERE (BranchID = @BranchID OR BranchID IS NULL)
                 AND (SupplierName LIKE '%' + @Search + '%' 
                      OR NoteNum LIKE '%' + @Search + '%' 
                      OR SupplierPhone LIKE '%' + @Search + '%')
               ORDER BY Purchases_Date DESC"
            : @"SELECT * FROM dbo.Contract_Contract 
               WHERE SupplierName LIKE '%' + @Search + '%' 
                 OR NoteNum LIKE '%' + @Search + '%' 
                 OR SupplierPhone LIKE '%' + @Search + '%'
               ORDER BY Purchases_Date DESC";

        return _db.QueryAsync(sql, p =>
        {
            p.Add("@Search", SqlDbType.NVarChar, 100).Value = term;
            if (branchId.HasValue)
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
        }, cancellationToken);
    }

    public async Task<int> CreateAsync(Contract_Contract contract, AppSession session, CancellationToken cancellationToken = default)
    {
        Validate(contract);

        const string sql = @"
INSERT INTO dbo.Contract_Contract (
    PurBranchID, BranchID, SupplierID, SupplierName, SupplierPhone, SupplierVatNum,
    Purchases_Date, Note, NoteNum, CostOrder, Tax, TotalPrices, Safy,
    DiscountNum, DiscountPerantage, Tax_Discount, TotalPrices_Discount, AllTax, Net,
    UserID_Add, UserBranch_Add, UserMacAddress_Add, UserDate_Add,
    BounceID, contractTerms
)
VALUES (
    @PurBranchID, @BranchID, @SupplierID, @SupplierName, @SupplierPhone, @SupplierVatNum,
    @Purchases_Date, @Note, @NoteNum, @CostOrder, @Tax, @TotalPrices, @Safy,
    @DiscountNum, @DiscountPerantage, @Tax_Discount, @TotalPrices_Discount, @AllTax, @Net,
    @UserID_Add, @UserBranch_Add, @UserMacAddress_Add, GETDATE(),
    @BounceID, @contractTerms
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var result = await _db.QueryAsync(sql, p =>
        {
            p.Add("@PurBranchID", SqlDbType.Int).Value = (object?)contract.PurBranchID ?? DBNull.Value;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)contract.BranchID ?? DBNull.Value;
            p.Add("@SupplierID", SqlDbType.Int).Value = (object?)contract.SupplierID ?? DBNull.Value;
            p.Add("@SupplierName", SqlDbType.NVarChar, 300).Value = (object?)contract.SupplierName ?? DBNull.Value;
            p.Add("@SupplierPhone", SqlDbType.NVarChar, 100).Value = (object?)contract.SupplierPhone ?? DBNull.Value;
            p.Add("@SupplierVatNum", SqlDbType.NVarChar, 100).Value = (object?)contract.SupplierVatNum ?? DBNull.Value;
            p.Add("@Purchases_Date", SqlDbType.Date).Value = (object?)contract.Purchases_Date ?? DBNull.Value;
            p.Add("@Note", SqlDbType.NVarChar, 1000).Value = (object?)contract.Note ?? DBNull.Value;
            p.Add("@NoteNum", SqlDbType.NVarChar, 100).Value = (object?)contract.NoteNum ?? DBNull.Value;
            p.Add("@CostOrder", SqlDbType.Decimal).Value = (object?)contract.CostOrder ?? DBNull.Value;
            p.Add("@Tax", SqlDbType.Decimal).Value = (object?)contract.Tax ?? DBNull.Value;
            p.Add("@TotalPrices", SqlDbType.Decimal).Value = (object?)contract.TotalPrices ?? DBNull.Value;
            p.Add("@Safy", SqlDbType.Decimal).Value = (object?)contract.Safy ?? DBNull.Value;
            p.Add("@DiscountNum", SqlDbType.Decimal).Value = (object?)contract.DiscountNum ?? DBNull.Value;
            p.Add("@DiscountPerantage", SqlDbType.Decimal).Value = (object?)contract.DiscountPerantage ?? DBNull.Value;
            p.Add("@Tax_Discount", SqlDbType.Decimal).Value = (object?)contract.Tax_Discount ?? DBNull.Value;
            p.Add("@TotalPrices_Discount", SqlDbType.Decimal).Value = (object?)contract.TotalPrices_Discount ?? DBNull.Value;
            p.Add("@AllTax", SqlDbType.Decimal).Value = (object?)contract.AllTax ?? DBNull.Value;
            p.Add("@Net", SqlDbType.Decimal).Value = (object?)contract.Net ?? DBNull.Value;
            p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Add", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = GetMachineMac();
            p.Add("@BounceID", SqlDbType.Int).Value = (object?)contract.BounceID ?? DBNull.Value;
            p.Add("@contractTerms", SqlDbType.NVarChar).Value = (object?)contract.contractTerms ?? DBNull.Value;
        }, cancellationToken);

        if (result.Rows.Count == 0)
            throw new InvalidOperationException(""); // "لم يتم إنشاء العقد"

        return Convert.ToInt32(result.Rows[0][0]);
    }

    public async Task<int> UpdateAsync(Contract_Contract contract, AppSession session, CancellationToken cancellationToken = default)
    {
        Validate(contract);

        const string sql = @"
UPDATE dbo.Contract_Contract
SET 
    PurBranchID = @PurBranchID,
    BranchID = @BranchID,
    SupplierID = @SupplierID,
    SupplierName = @SupplierName,
    SupplierPhone = @SupplierPhone,
    SupplierVatNum = @SupplierVatNum,
    Purchases_Date = @Purchases_Date,
    Note = @Note,
    NoteNum = @NoteNum,
    CostOrder = @CostOrder,
    Tax = @Tax,
    TotalPrices = @TotalPrices,
    Safy = @Safy,
    DiscountNum = @DiscountNum,
    DiscountPerantage = @DiscountPerantage,
    Tax_Discount = @Tax_Discount,
    TotalPrices_Discount = @TotalPrices_Discount,
    AllTax = @AllTax,
    Net = @Net,
    UserID_Update = @UserID_Update,
    UserBranch_Update = @UserBranch_Update,
    UserMacAddress_Update = @UserMacAddress_Update,
    UserDate_Update = GETDATE(),
    BounceID = @BounceID,
    contractTerms = @contractTerms
WHERE ID = @ID;";

        return await _db.ExecuteAsync(sql, p =>
        {
            p.Add("@ID", SqlDbType.Int).Value = contract.ID;
            p.Add("@PurBranchID", SqlDbType.Int).Value = (object?)contract.PurBranchID ?? DBNull.Value;
            p.Add("@BranchID", SqlDbType.Int).Value = (object?)contract.BranchID ?? DBNull.Value;
            p.Add("@SupplierID", SqlDbType.Int).Value = (object?)contract.SupplierID ?? DBNull.Value;
            p.Add("@SupplierName", SqlDbType.NVarChar, 300).Value = (object?)contract.SupplierName ?? DBNull.Value;
            p.Add("@SupplierPhone", SqlDbType.NVarChar, 100).Value = (object?)contract.SupplierPhone ?? DBNull.Value;
            p.Add("@SupplierVatNum", SqlDbType.NVarChar, 100).Value = (object?)contract.SupplierVatNum ?? DBNull.Value;
            p.Add("@Purchases_Date", SqlDbType.Date).Value = (object?)contract.Purchases_Date ?? DBNull.Value;
            p.Add("@Note", SqlDbType.NVarChar, 1000).Value = (object?)contract.Note ?? DBNull.Value;
            p.Add("@NoteNum", SqlDbType.NVarChar, 100).Value = (object?)contract.NoteNum ?? DBNull.Value;
            p.Add("@CostOrder", SqlDbType.Decimal).Value = (object?)contract.CostOrder ?? DBNull.Value;
            p.Add("@Tax", SqlDbType.Decimal).Value = (object?)contract.Tax ?? DBNull.Value;
            p.Add("@TotalPrices", SqlDbType.Decimal).Value = (object?)contract.TotalPrices ?? DBNull.Value;
            p.Add("@Safy", SqlDbType.Decimal).Value = (object?)contract.Safy ?? DBNull.Value;
            p.Add("@DiscountNum", SqlDbType.Decimal).Value = (object?)contract.DiscountNum ?? DBNull.Value;
            p.Add("@DiscountPerantage", SqlDbType.Decimal).Value = (object?)contract.DiscountPerantage ?? DBNull.Value;
            p.Add("@Tax_Discount", SqlDbType.Decimal).Value = (object?)contract.Tax_Discount ?? DBNull.Value;
            p.Add("@TotalPrices_Discount", SqlDbType.Decimal).Value = (object?)contract.TotalPrices_Discount ?? DBNull.Value;
            p.Add("@AllTax", SqlDbType.Decimal).Value = (object?)contract.AllTax ?? DBNull.Value;
            p.Add("@Net", SqlDbType.Decimal).Value = (object?)contract.Net ?? DBNull.Value;
            p.Add("@UserID_Update", SqlDbType.Int).Value = session.UserId;
            p.Add("@UserBranch_Update", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
            p.Add("@UserMacAddress_Update", SqlDbType.NVarChar, 200).Value = GetMachineMac();
            p.Add("@BounceID", SqlDbType.Int).Value = (object?)contract.BounceID ?? DBNull.Value;
            p.Add("@contractTerms", SqlDbType.NVarChar).Value = (object?)contract.contractTerms ?? DBNull.Value;
        }, cancellationToken);
    }

    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => _db.ExecuteAsync("DELETE FROM dbo.Contract_Contract WHERE ID = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id, cancellationToken);

    public Task<Contract_Contract?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.QuerySingleAsync<Contract_Contract>(
            "SELECT * FROM dbo.Contract_Contract WHERE ID = @ID;",
            p => p.Add("@ID", SqlDbType.Int).Value = id, cancellationToken);

    private static void Validate(Contract_Contract contract)
    {
        if (string.IsNullOrWhiteSpace(contract.SupplierName))
            throw new ArgumentException(""); // "اسم المورد/العميل مطلوب"
        if (contract.SupplierName.Trim().Length > 300)
            throw new ArgumentException(" 300 "); // "اسم المورد/العميل طويل جدا"
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
