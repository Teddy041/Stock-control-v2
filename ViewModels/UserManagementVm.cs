namespace StockControl.ViewModels;

public class UserManagementVm
{
    public string? Query { get; set; }
    public List<UserManagementRowVm> Users { get; set; } = new();
    public CreateMemberVm CreateMember { get; set; } = new();
}

public class UserManagementRowVm
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsRootUser { get; set; }
    public bool CanManageProducts { get; set; }
    public bool CanUpdateStock { get; set; }
    public bool CanViewLogs { get; set; }
    public bool CanManageCategories { get; set; }
    public bool CanHandleStockRequests { get; set; }
    public bool CanManageUsers { get; set; }
}

public class CreateMemberVm
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
