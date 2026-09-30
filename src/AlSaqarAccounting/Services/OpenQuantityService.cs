using System.Data;
using System.Data.SqlClient;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Full operational service for opening item quantities. The insert/update/delete
/// contracts are confirmed in GTSdb2026 and use dbo.Items_OpenQuantity as TVP.
/// </summary>
public sealed class OpenQuantityService
{
    private readonly DbExecutor _db;

    public OpenQuantityService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(int? branchId, CancellationToken cancellationToken = default)
        => ExecuteBranchAsync("dbo.Select_Order_OpenQuantity", branchId, cancellationToken);

    public Task<DataTable> ListItemsAsync(CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync("dbo.Get_All_Items", cancellationToken: cancellationToken);

    public Task<DataTable> ListDetailsAsync(int id, int? branchId, CancellationToken cancellationToken = default)
    {
        if (!branchId.HasValue)
            throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");

        return _db.QueryAsync(
            @"
SELECT SN,
       Purchese_ID,
       ItemID,
       BranchID,
       StoreID,
       ItemUnitID,
       Quantity,
       UnitPrice,
       TotalPrice,
       ItemUnitType,
       UnitNumber,
       SellPrice
FROM dbo.Order_OpenQuantityDetails
WHERE Purchese_ID = @PurBranchID
  AND BranchID = @BranchID
ORDER BY SN;",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = id;
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            },
            cancellationToken);
    }

    public Task<DataTable> PrintAsync(int id, int? branchId, CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync(
            "dbo.Print_Order_OpenQuantity",
            p =>
            {
                p.Add("@ID", SqlDbType.Int).Value = id;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
            },
            cancellationToken);

    public async Task CreateAsync(
        OpenQuantityDocument document,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        Validate(document, session);

        var items = await TvpTableBuilder.CreateAsync(
            _db, "Items_OpenQuantity", document.Lines.Cast<object>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        await _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Insert_OpenQuantity",
            p =>
            {
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId!.Value;
                p.Add("@Purchases_Date", SqlDbType.DateTime).Value = document.DocumentDate;
                p.Add("@NoteNum", SqlDbType.NVarChar, 100).Value =
                    (object?)NullIfEmpty(document.NoteNum) ?? DBNull.Value;
                p.Add("@TotalPrices", SqlDbType.Decimal).Value = document.TotalPrices;
                p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
                p.Add("@UserBranch_Add", SqlDbType.Int).Value = session.BranchId.Value;
                p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                var tvp = p.Add("@Items", SqlDbType.Structured);
                tvp.TypeName = "dbo.Items_OpenQuantity";
                tvp.Value = items;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        int id,
        OpenQuantityDocument document,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        Validate(document, session);

        var items = await TvpTableBuilder.CreateAsync(
            _db, "Items_OpenQuantity", document.Lines.Cast<object>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        await _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Update_OpenQuantity",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = id;
                p.Add("@BranchID", SqlDbType.Int).Value = session.BranchId!.Value;
                p.Add("@Purchases_Date", SqlDbType.DateTime).Value = document.DocumentDate;
                p.Add("@NoteNum", SqlDbType.NVarChar, 100).Value =
                    (object?)NullIfEmpty(document.NoteNum) ?? DBNull.Value;
                p.Add("@TotalPrices", SqlDbType.Decimal).Value = document.TotalPrices;
                p.Add("@UserID_Add", SqlDbType.Int).Value = session.UserId;
                p.Add("@UserBranch_Add", SqlDbType.Int).Value = session.BranchId.Value;
                p.Add("@UserMacAddress_Add", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                var tvp = p.Add("@Items", SqlDbType.Structured);
                tvp.TypeName = "dbo.Items_OpenQuantity";
                tvp.Value = items;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(int id, int? branchId, CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Delete_Order_OpenQuantity",
            p =>
            {
                p.Add("@PurBranchID", SqlDbType.Int).Value = id;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
            },
            cancellationToken);

    private static void Validate(OpenQuantityDocument document, AppSession session)
    {
        if (!session.BranchId.HasValue)
            throw new InvalidOperationException("حفظ الكميات الافتتاحية يتطلب فرعاً فعّالاً.");

        if (document.Lines.Count == 0)
            throw new ArgumentException("أضف صنفاً واحداً على الأقل.");

        foreach (var line in document.Lines)
        {
            if (line.ItemID is null || line.ItemID <= 0)
                throw new ArgumentException("رقم الصنف يجب أن يكون صحيحاً.");
            if (line.StoreID is null || line.StoreID <= 0)
                throw new ArgumentException("رقم المخزن يجب أن يكون صحيحاً.");
            if ((line.Quantity ?? 0) <= 0)
                throw new ArgumentException("الكمية يجب أن تكون أكبر من صفر.");
            if ((line.UnitPrice ?? 0) < 0 || (line.SellPrice ?? 0) < 0)
                throw new ArgumentException("الأسعار لا يمكن أن تكون سالبة.");
        }
    }

    private Task<DataTable> ExecuteBranchAsync(
        string procedureName,
        int? branchId,
        CancellationToken cancellationToken)
        => _db.ExecuteStoredProcedureAsync(
            procedureName,
            p =>
            {
                if (!branchId.HasValue)
                    throw new InvalidOperationException("هذه الشاشة تحتاج إلى فرع فعّال.");
                p.Add("@BranchID", SqlDbType.Int).Value = branchId.Value;
            },
            cancellationToken);

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OpenQuantityDocument
{
    public DateTime DocumentDate { get; set; } = DateTime.Now;
    public string? NoteNum { get; set; }
    public decimal TotalPrices => Lines.Sum(x => x.TotalPrice ?? 0m);
    public List<Order_OpenQuantityDetails> Lines { get; } = new();
}
