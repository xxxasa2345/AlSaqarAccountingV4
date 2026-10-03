using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using AlSaqarAccounting.Core;
using AlSaqarAccounting.Models;

namespace AlSaqarAccounting.Services;

/// <summary>
/// Real inventory-item service. Reads through the original Get_All_Items
/// procedure and inserts through the original Insert_Items TVP procedure.
/// </summary>
public sealed class ItemsService
{
    private readonly DbExecutor _db;
    private readonly AuthorizationService _authorization;

    public ItemsService(DbExecutor db)
    {
        _db = db;
        _authorization = new AuthorizationService(db);
    }

    public Task<DataTable> ListAsync(CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync("dbo.Get_All_Items", cancellationToken: cancellationToken);

    public Task<DataTable> ListStockAsync(
        int itemId,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
            throw new ArgumentException("معرف الصنف غير صالح.", nameof(itemId));

        var sql = @"
SELECT
    s.ID AS StoreID,
    s.Store_Name AS StoreName,
    s.BranchID,
    ISNULL(q.OpeningBalance, 0) AS OpeningBalance,
    ISNULL(q.CurrentBalance, 0) AS CurrentBalance,
    ISNULL(q.BeginningInventory, 0) AS BeginningInventory,
    ISNULL(q.BeginningInventoryPrice, 0) AS BeginningInventoryPrice,
    ISNULL(q.UnitNumber, 0) AS UnitNumber
FROM dbo.Account_Stores AS s
LEFT JOIN dbo.ItemQuantity AS q
    ON q.StoreID = s.ID
   AND q.ItemID = @ItemId
WHERE (@BranchID IS NULL OR s.BranchID = @BranchID OR s.BranchID IS NULL)
ORDER BY s.Store_Name, s.ID;";

        return _db.QueryAsync(
            sql,
            p =>
            {
                p.Add("@ItemId", SqlDbType.Int).Value = itemId;
                p.Add("@BranchID", SqlDbType.Int).Value =
                    (object?)session.BranchId ?? DBNull.Value;
            },
            cancellationToken);
    }

    public async Task CreateAsync(
        Item_Items item,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        Validate(item);

        await _authorization.RequireAsync(
            session, screenId, PermissionAction.Save, cancellationToken).ConfigureAwait(false);

        var tvp = CreateItemTable(item, session);
        await _db.ExecuteStoredProcedureNonQueryAsync(
            "dbo.Insert_Items",
            p =>
            {
                var parameter = p.Add("@Items", SqlDbType.Structured);
                parameter.TypeName = "dbo.Item_Items";
                parameter.Value = tvp;
            },
            cancellationToken);
    }

    public async Task<int> UpdateAsync(
        int itemId,
        Item_Items item,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
            throw new ArgumentException("معرف الصنف غير صالح.", nameof(itemId));

        Validate(item);

        await _authorization.RequireAsync(
            session, screenId, PermissionAction.Edit, cancellationToken).ConfigureAwait(false);

        return await _db.ExecuteAsync(
            @"UPDATE dbo.Item_Items
              SET Item_code = @ItemCode,
                  item_Name = @ItemName,
                  item_Name_English = @EnglishName,
                  UnitSmall = @UnitSmall,
                  UnitMedium = @UnitMedium,
                  UnitLarge = @UnitLarge,
                  SellPriceSmall = @SellPriceSmall,
                  Is_Tax = @IsTax,
                  Tax_Value = @TaxValue,
                  UserID_Update = @UserID,
                  UserBranch_Update = @BranchID,
                  UserMacAddress_Update = @Mac,
                  UserDate_Update = GETDATE()
              WHERE ItemId = @ItemId;",
            p =>
            {
                p.Add("@ItemId", SqlDbType.Int).Value = itemId;
                p.Add("@ItemCode", SqlDbType.NVarChar, 100).Value = item.Item_code.Trim();
                p.Add("@ItemName", SqlDbType.NVarChar, 300).Value = item.item_Name.Trim();
                p.Add("@EnglishName", SqlDbType.NVarChar, 300).Value = (object?)item.item_Name_English ?? DBNull.Value;
                p.Add("@UnitSmall", SqlDbType.Int).Value = (object?)item.UnitSmall ?? DBNull.Value;
                p.Add("@UnitMedium", SqlDbType.Int).Value = (object?)item.UnitMedium ?? DBNull.Value;
                p.Add("@UnitLarge", SqlDbType.Int).Value = (object?)item.UnitLarge ?? DBNull.Value;
                p.Add("@SellPriceSmall", SqlDbType.Decimal).Value = (object?)item.SellPriceSmall ?? DBNull.Value;
                p.Add("@IsTax", SqlDbType.Bit).Value = (object?)item.Is_Tax ?? DBNull.Value;
                p.Add("@TaxValue", SqlDbType.Decimal).Value = (object?)item.Tax_Value ?? DBNull.Value;
                p.Add("@UserID", SqlDbType.Int).Value = session.UserId;
                p.Add("@BranchID", SqlDbType.Int).Value = (object?)session.BranchId ?? DBNull.Value;
                p.Add("@Mac", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(
        int itemId,
        AppSession session,
        int screenId,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
            throw new ArgumentException("معرف الصنف غير صالح.", nameof(itemId));

        await _authorization.RequireAsync(
            session, screenId, PermissionAction.Delete, cancellationToken).ConfigureAwait(false);

        var refs = await _db.QueryAsync(
            @"SELECT
                  (SELECT COUNT(1) FROM dbo.ItemQuantity WHERE ItemID = @ItemId) +
                  (SELECT COUNT(1) FROM dbo.Order_OrdersDetails WHERE ItemID = @ItemId) +
                  (SELECT COUNT(1) FROM dbo.Order_PurchasesDetails WHERE ItemID = @ItemId) +
                  (SELECT COUNT(1) FROM dbo.Item_ItemComponent
                   WHERE ItemID_Master = @ItemId OR ItemID_Complant = @ItemId) AS RefCount;",
            p => p.Add("@ItemId", SqlDbType.Int).Value = itemId,
            cancellationToken).ConfigureAwait(false);

        if (refs.Rows.Count > 0 && Convert.ToInt32(refs.Rows[0]["RefCount"]) > 0)
            throw new InvalidOperationException(
                "لا يمكن حذف الصنف لأنه مستخدم في حركات أو أرصدة مخزنية. استخدم التعطيل/الإيقاف بدلاً من الحذف.");

        return await _db.ExecuteAsync(
            "DELETE FROM dbo.Item_Items WHERE ItemId = @ItemId;",
            p => p.Add("@ItemId", SqlDbType.Int).Value = itemId,
            cancellationToken).ConfigureAwait(false);
    }

    private static DataTable CreateItemTable(Item_Items item, AppSession session)
    {
        var table = new DataTable("Item_Items");
        var properties = typeof(Item_Items).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            table.Columns.Add(property.Name, type);
        }

        var row = table.NewRow();
        foreach (var property in properties)
        {
            var value = property.GetValue(item);
            row[property.Name] = value ?? DBNull.Value;
        }

        row[nameof(Item_Items.UserID_Add)] = session.UserId;
        row[nameof(Item_Items.UserBranch_Add)] = (object?)session.BranchId ?? DBNull.Value;
        row[nameof(Item_Items.UserMacAddress_Add)] = Environment.MachineName;
        row[nameof(Item_Items.UserDate_Add)] = DateTime.Now;
        table.Rows.Add(row);
        return table;
    }

    private static void Validate(Item_Items item)
    {
        if (string.IsNullOrWhiteSpace(item.Item_code))
            throw new ArgumentException("كود الصنف مطلوب.");
        if (string.IsNullOrWhiteSpace(item.item_Name))
            throw new ArgumentException("اسم الصنف مطلوب.");
        if (item.Item_code.Trim().Length > 100)
            throw new ArgumentException("كود الصنف طويل أكثر من الحد المسموح.");
        if (item.item_Name.Trim().Length > 300)
            throw new ArgumentException("اسم الصنف طويل أكثر من الحد المسموح.");
    }
}
