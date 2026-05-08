using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StockControl.Data;
using StockControl.Models;
using StockControl.ViewModels;

namespace StockControl.Controllers;

[Authorize]
public class UserManagementController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    AppDbContext context) : Controller
{
    private const string RootUserName = "admin123";

    private static bool IsRootUser(string? userName)
    {
        return string.Equals(userName, RootUserName, StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        if (!User.HasPermission(Permissions.ManageUsers))
        {
            return Forbid();
        }

        var usersQuery = userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            usersQuery = usersQuery.Where(u => (u.UserName ?? "").ToLower().Contains(term));
        }

        var users = usersQuery.OrderBy(u => u.UserName).ToList();
        var rows = new List<UserManagementRowVm>();
        foreach (var user in users)
        {
            var isRoot = IsRootUser(user.UserName);
            var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
            var claims = await userManager.GetClaimsAsync(user);
            rows.Add(new UserManagementRowVm
            {
                UserId = user.Id,
                UserName = user.UserName ?? "(isimsiz)",
                IsAdmin = isAdmin,
                IsRootUser = isRoot,
                CanManageProducts = isAdmin || claims.Any(c => c.Type == Permissions.ClaimType && c.Value == Permissions.ManageProducts),
                CanUpdateStock = isAdmin || claims.Any(c => c.Type == Permissions.ClaimType && c.Value == Permissions.UpdateStock),
                CanViewLogs = isAdmin || claims.Any(c => c.Type == Permissions.ClaimType && c.Value == Permissions.ViewLogs),
                CanManageCategories = isAdmin || claims.Any(c => c.Type == Permissions.ClaimType && c.Value == Permissions.ManageCategories),
                CanHandleStockRequests = isAdmin || claims.Any(c => c.Type == Permissions.ClaimType && c.Value == Permissions.HandleStockRequests),
                CanManageUsers = isAdmin || claims.Any(c => c.Type == Permissions.ClaimType && c.Value == Permissions.ManageUsers)
            });
        }

        return View(new UserManagementVm
        {
            Query = q,
            Users = rows
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateMemberVm vm)
    {
        if (!User.HasPermission(Permissions.ManageUsers))
        {
            return Forbid();
        }

        var userName = vm.UserName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userName) || userName.Length < 3)
        {
            TempData["UserMgmtError"] = "Kullanici adi en az 3 karakter olmali.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(vm.Password) || vm.Password.Length < 6)
        {
            TempData["UserMgmtError"] = "Sifre en az 6 karakter olmali.";
            return RedirectToAction(nameof(Index));
        }

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            TempData["UserMgmtError"] = "Bu kullanici adi zaten kullaniliyor.";
            return RedirectToAction(nameof(Index));
        }

        var user = new IdentityUser { UserName = userName };
        var result = await userManager.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            TempData["UserMgmtError"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        await userManager.AddToRoleAsync(user, "User");
        var actor = User.Identity?.Name ?? "admin";
        context.AppendActivity(
            actor,
            ActivityLogExtensions.ResolveActorRole(User),
            ActivityEventType.UserCreated,
            $"Yeni uye olusturuldu: {userName}",
            $"Olusturan: {actor}",
            null);
        await context.SaveChangesAsync();
        TempData["UserMgmtSuccess"] = "Yeni uye olusturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePermissions(UserManagementRowVm vm)
    {
        if (!User.HasPermission(Permissions.ManageUsers))
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(vm.UserId);
        if (user is null)
        {
            TempData["UserMgmtError"] = "Kullanici bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        if (await userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["UserMgmtError"] = "Admin hesaplarinin izinleri buradan degistirilemez.";
            return RedirectToAction(nameof(Index));
        }
        if (IsRootUser(user.UserName))
        {
            TempData["UserMgmtError"] = "admin123 hesabi korunuyor, yetkileri degistirilemez.";
            return RedirectToAction(nameof(Index));
        }

        var actor = User.Identity?.Name ?? "admin";
        var currentClaims = await userManager.GetClaimsAsync(user);
        var currentPermissions = currentClaims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (vm.CanManageProducts) targetPermissions.Add(Permissions.ManageProducts);
        if (vm.CanUpdateStock) targetPermissions.Add(Permissions.UpdateStock);
        if (vm.CanViewLogs) targetPermissions.Add(Permissions.ViewLogs);
        if (vm.CanManageCategories) targetPermissions.Add(Permissions.ManageCategories);
        if (vm.CanHandleStockRequests) targetPermissions.Add(Permissions.HandleStockRequests);
        if (vm.CanManageUsers) targetPermissions.Add(Permissions.ManageUsers);

        var added = targetPermissions.Except(currentPermissions).OrderBy(x => x).ToList();
        var removed = currentPermissions.Except(targetPermissions).OrderBy(x => x).ToList();

        var permClaims = currentClaims.Where(c => c.Type == Permissions.ClaimType).ToList();
        foreach (var claim in permClaims)
        {
            await userManager.RemoveClaimAsync(user, claim);
        }

        await AddPermissionIfChecked(user, Permissions.ManageProducts, vm.CanManageProducts);
        await AddPermissionIfChecked(user, Permissions.UpdateStock, vm.CanUpdateStock);
        await AddPermissionIfChecked(user, Permissions.ViewLogs, vm.CanViewLogs);
        await AddPermissionIfChecked(user, Permissions.ManageCategories, vm.CanManageCategories);
        await AddPermissionIfChecked(user, Permissions.HandleStockRequests, vm.CanHandleStockRequests);
        await AddPermissionIfChecked(user, Permissions.ManageUsers, vm.CanManageUsers);
        await userManager.UpdateSecurityStampAsync(user);

        if (added.Count > 0 || removed.Count > 0)
        {
            var details = $"Degistiren: {actor}; Eklenen: {(added.Count == 0 ? "-" : string.Join(", ", added))}; Kaldirilan: {(removed.Count == 0 ? "-" : string.Join(", ", removed))}";
            context.AppendActivity(
                actor,
                ActivityLogExtensions.ResolveActorRole(User),
                ActivityEventType.UserPermissionsUpdated,
                $"Uye yetkileri guncellendi: {user.UserName}",
                details,
                null);
            await context.SaveChangesAsync();
        }

        if (string.Equals(user.UserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            await signInManager.RefreshSignInAsync(user);
        }

        TempData["UserMgmtSuccess"] = $"{user.UserName} icin yetkiler guncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAllPermissions(string userId, bool grantAll)
    {
        var vm = new UserManagementRowVm
        {
            UserId = userId,
            CanManageProducts = grantAll,
            CanUpdateStock = grantAll,
            CanViewLogs = grantAll,
            CanManageCategories = grantAll,
            CanHandleStockRequests = grantAll,
            CanManageUsers = grantAll
        };

        return await UpdatePermissions(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string userId, string newPassword)
    {
        if (!User.HasPermission(Permissions.ManageUsers))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["UserMgmtError"] = "Yeni sifre en az 6 karakter olmali.";
            return RedirectToAction(nameof(Index));
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["UserMgmtError"] = "Kullanici bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        if (IsRootUser(user.UserName))
        {
            TempData["UserMgmtError"] = "admin123 hesabinin sifresi panelden degistirilemez.";
            return RedirectToAction(nameof(Index));
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!resetResult.Succeeded)
        {
            TempData["UserMgmtError"] = string.Join(" | ", resetResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }
        await userManager.UpdateSecurityStampAsync(user);

        var actor = User.Identity?.Name ?? "admin";
        context.AppendActivity(
            actor,
            ActivityLogExtensions.ResolveActorRole(User),
            ActivityEventType.UserPasswordReset,
            $"Uye sifresi sifirlandi: {user.UserName}",
            $"Degistiren: {actor}",
            null);
        await context.SaveChangesAsync();

        TempData["UserMgmtSuccess"] = $"{user.UserName} sifresi sifirlandi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        if (!User.HasPermission(Permissions.ManageUsers))
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["UserMgmtError"] = "Kullanici bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        if (await userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["UserMgmtError"] = "Admin hesabi silinemez.";
            return RedirectToAction(nameof(Index));
        }
        if (IsRootUser(user.UserName))
        {
            TempData["UserMgmtError"] = "admin123 hesabi silinemez.";
            return RedirectToAction(nameof(Index));
        }

        var actor = User.Identity?.Name ?? "admin";
        if (string.Equals(user.UserName, actor, StringComparison.OrdinalIgnoreCase))
        {
            TempData["UserMgmtError"] = "Kendi hesabinizi silemezsiniz.";
            return RedirectToAction(nameof(Index));
        }

        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            TempData["UserMgmtError"] = string.Join(" | ", deleteResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        context.AppendActivity(
            actor,
            ActivityLogExtensions.ResolveActorRole(User),
            ActivityEventType.UserDeleted,
            $"Uye silindi: {user.UserName}",
            $"Silen: {actor}",
            null);
        await context.SaveChangesAsync();

        TempData["UserMgmtSuccess"] = $"{user.UserName} silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task AddPermissionIfChecked(IdentityUser user, string permission, bool isChecked)
    {
        if (!isChecked)
        {
            return;
        }

        await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(Permissions.ClaimType, permission));
    }
}
