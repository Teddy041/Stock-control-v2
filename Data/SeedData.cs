using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockControl.Models;

namespace StockControl.Data;

public static class SeedData
{
    public static async Task InitializeAsync(
        AppDbContext context,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        var roles = new[] { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin123", "admin123", "Admin");
        await EnsureUserAsync(userManager, "user123", "user123", "User");

        var defaultCategories = new[] { "Icecek", "Gida", "Mutfak", "Temizlik", "Elektronik", "Diger" };
        foreach (var categoryName in defaultCategories)
        {
            if (!await context.Categories.AnyAsync(c => c.Name == categoryName))
            {
                context.Categories.Add(new Category { Name = categoryName });
            }
        }
        await context.SaveChangesAsync();

        if (context.Products.Any())
        {
            return;
        }

        var products = new List<Product>
        {
            new() { Name = "Filtre Kahve", Code = "PRD-001", Category = "Icecek", UnitPrice = 120m, Quantity = 30 },
            new() { Name = "Yesil Cay", Code = "PRD-002", Category = "Icecek", UnitPrice = 90m, Quantity = 20 },
            new() { Name = "Toz Seker", Code = "PRD-003", Category = "Gida", UnitPrice = 45m, Quantity = 50 },
            new() { Name = "Cam Bardak", Code = "PRD-004", Category = "Mutfak", UnitPrice = 35m, Quantity = 40 }
        };

        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(
        UserManager<IdentityUser> userManager,
        string userName,
        string password,
        string role)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = userName,
                Email = $"{userName}@stock.local",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            await userManager.ResetPasswordAsync(user, resetToken, password);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
