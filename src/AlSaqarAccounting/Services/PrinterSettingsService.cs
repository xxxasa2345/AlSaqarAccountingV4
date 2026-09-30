using System.Data;
using AlSaqarAccounting.Core;

namespace AlSaqarAccounting.Services;

public sealed class PrinterSettingsService
{
    private readonly DbExecutor _db;
    public PrinterSettingsService(DbExecutor db) => _db = db;

    public Task<DataTable> ListPrintersAsync(int? branchId, CancellationToken ct = default) =>
        _db.QueryAsync(@"SELECT ID,Name,BranchID,IPAddress,PortNum FROM dbo.Printers
                         WHERE @BranchID IS NULL OR BranchID=@BranchID ORDER BY Name,ID;",
            p => p.Add("@BranchID", System.Data.SqlDbType.Int).Value=(object?)branchId??DBNull.Value, ct);

    public Task<DataTable> ListItemsAsync(CancellationToken ct = default) =>
        _db.QueryAsync("SELECT ItemID,Item_code,item_Name FROM dbo.Item_Items ORDER BY item_Name,ItemID;",
            cancellationToken:ct);

    public Task<DataTable> ListCookMappingsAsync(int? branchId, CancellationToken ct = default) =>
        _db.QueryAsync(@"SELECT pc.ID,pc.BranchID,pc.PrinterID,p.Name PrinterName,p.IPAddress,p.PortNum,
                                pc.ItemID,i.Item_code,i.item_Name
                         FROM dbo.PrintersCook pc
                         LEFT JOIN dbo.Printers p ON p.ID=pc.PrinterID
                         LEFT JOIN dbo.Item_Items i ON i.ItemID=pc.ItemID
                         WHERE @BranchID IS NULL OR pc.BranchID=@BranchID
                         ORDER BY p.Name,i.item_Name,pc.ID;",
            p => p.Add("@BranchID",System.Data.SqlDbType.Int).Value=(object?)branchId??DBNull.Value, ct);

    public async Task<int> SavePrinterAsync(string name,string ip,string port,int? branchId,int? id,CancellationToken ct=default)
    {
        if(!branchId.HasValue) throw new InvalidOperationException("الفرع الفعال مطلوب.");
        if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("اسم الطابعة مطلوب.");
        if(id.HasValue)
        {
            await _db.ExecuteAsync(@"UPDATE dbo.Printers SET Name=@Name,IPAddress=@IPAddress,PortNum=@PortNum
                                     WHERE ID=@ID AND BranchID=@BranchID;",
                p=>{p.Add("@Name",System.Data.SqlDbType.NVarChar,500).Value=name.Trim();
                     p.Add("@IPAddress",System.Data.SqlDbType.NVarChar,100).Value=(object?)ip?.Trim()??DBNull.Value;
                     p.Add("@PortNum",System.Data.SqlDbType.NVarChar,100).Value=(object?)port?.Trim()??DBNull.Value;
                     p.Add("@ID",System.Data.SqlDbType.Int).Value=id.Value;
                     p.Add("@BranchID",System.Data.SqlDbType.Int).Value=branchId.Value;},ct);
            return id.Value;
        }
        var dt=await _db.QueryAsync("SELECT ISNULL(MAX(ID),0)+1 NextID FROM dbo.Printers;",cancellationToken:ct);
        var newId=Convert.ToInt32(dt.Rows[0]["NextID"]);
        await _db.ExecuteAsync(@"INSERT INTO dbo.Printers(ID,Name,BranchID,IPAddress,PortNum)
                                 VALUES(@ID,@Name,@BranchID,@IPAddress,@PortNum);",
            p=>{p.Add("@ID",System.Data.SqlDbType.Int).Value=newId;
                 p.Add("@Name",System.Data.SqlDbType.NVarChar,500).Value=name.Trim();
                 p.Add("@BranchID",System.Data.SqlDbType.Int).Value=branchId.Value;
                 p.Add("@IPAddress",System.Data.SqlDbType.NVarChar,100).Value=(object?)ip?.Trim()??DBNull.Value;
                 p.Add("@PortNum",System.Data.SqlDbType.NVarChar,100).Value=(object?)port?.Trim()??DBNull.Value;},ct);
        return newId;
    }

    public Task DeletePrinterAsync(int id,int? branchId,CancellationToken ct=default) =>
        _db.ExecuteAsync("DELETE FROM dbo.Printers WHERE ID=@ID AND BranchID=@BranchID;",
            p=>{p.Add("@ID",System.Data.SqlDbType.Int).Value=id;p.Add("@BranchID",System.Data.SqlDbType.Int).Value=(object?)branchId??DBNull.Value;},ct);

    public async Task<int> AddCookMappingAsync(int printerId,int itemId,int? branchId,CancellationToken ct=default)
    {
        if(!branchId.HasValue) throw new InvalidOperationException("الفرع الفعال مطلوب.");
        var exists=await _db.QueryAsync(@"SELECT TOP(1) ID FROM dbo.PrintersCook
            WHERE PrinterID=@PrinterID AND ItemID=@ItemID AND BranchID=@BranchID;",
            p=>{p.Add("@PrinterID",System.Data.SqlDbType.Int).Value=printerId;p.Add("@ItemID",System.Data.SqlDbType.Int).Value=itemId;p.Add("@BranchID",System.Data.SqlDbType.Int).Value=branchId.Value;},ct);
        if(exists.Rows.Count>0)return Convert.ToInt32(exists.Rows[0]["ID"]);
        var next=await _db.QueryAsync("SELECT ISNULL(MAX(ID),0)+1 NextID FROM dbo.PrintersCook;",cancellationToken:ct);
        var id=Convert.ToInt32(next.Rows[0]["NextID"]);
        await _db.ExecuteAsync(@"INSERT INTO dbo.PrintersCook(ID,PrinterID,ItemID,BranchID)
                                 VALUES(@ID,@PrinterID,@ItemID,@BranchID);",
            p=>{p.Add("@ID",System.Data.SqlDbType.Int).Value=id;p.Add("@PrinterID",System.Data.SqlDbType.Int).Value=printerId;
                 p.Add("@ItemID",System.Data.SqlDbType.Int).Value=itemId;p.Add("@BranchID",System.Data.SqlDbType.Int).Value=branchId.Value;},ct);
        return id;
    }

    public Task DeleteCookMappingAsync(int id,int? branchId,CancellationToken ct=default) =>
        _db.ExecuteAsync("DELETE FROM dbo.PrintersCook WHERE ID=@ID AND BranchID=@BranchID;",
            p=>{p.Add("@ID",System.Data.SqlDbType.Int).Value=id;p.Add("@BranchID",System.Data.SqlDbType.Int).Value=(object?)branchId??DBNull.Value;},ct);
}