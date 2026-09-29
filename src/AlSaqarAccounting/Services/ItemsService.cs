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

    public ItemsService(DbExecutor db) => _db = db;

    public Task<DataTable> ListAsync(CancellationToken cancellationToken = default)
        => _db.ExecuteStoredProcedureAsync("dbo.Get_All_Items", cancellationToken: cancellationToken);

    public async Task CreateAsync(
        Item_Items item,
        AppSession session,
        CancellationToken cancellationToken = default)
    {
        Validate(item);

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
