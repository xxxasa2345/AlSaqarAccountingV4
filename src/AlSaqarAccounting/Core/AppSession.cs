namespace AlSaqarAccounting.Core;

public sealed class AppSession
{
    public int UserId { get; init; }
    public string UserName { get; init; } = "";
    public int? BranchId { get; init; }
    public int? GroupId { get; init; }
    public bool IsOpenDay { get; init; }
}
