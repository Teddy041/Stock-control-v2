using System.Security.Claims;

namespace StockControl.Data;

public static class Permissions
{
    public const string ClaimType = "perm";

    public const string ManageProducts = "manage_products";
    public const string UpdateStock = "update_stock";
    public const string ViewLogs = "view_logs";
    public const string ManageCategories = "manage_categories";
    public const string HandleStockRequests = "handle_stock_requests";
    public const string ManageUsers = "manage_users";

    public static readonly string[] All =
    [
        ManageProducts,
        UpdateStock,
        ViewLogs,
        ManageCategories,
        HandleStockRequests,
        ManageUsers
    ];
}

public static class PermissionExtensions
{
    public static bool HasPermission(this ClaimsPrincipal user, string permission)
    {
        return user.IsInRole("Admin") || user.HasClaim(Permissions.ClaimType, permission);
    }
}
