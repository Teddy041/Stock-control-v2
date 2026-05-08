using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockControl.Data;
using StockControl.Models;

namespace StockControl.Controllers;

[Authorize]
public class CategoriesController(AppDbContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.HasPermission(Permissions.ManageCategories))
        {
            return Forbid();
        }

        var categories = await context.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (!User.HasPermission(Permissions.ManageCategories))
        {
            return Forbid();
        }

        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            TempData["CategoryError"] = "Kategori adi bos olamaz.";
            return RedirectToAction(nameof(Index));
        }

        var exists = await context.Categories.AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower());
        if (exists)
        {
            TempData["CategoryError"] = "Bu kategori zaten mevcut.";
            return RedirectToAction(nameof(Index));
        }

        context.Categories.Add(new Category { Name = trimmedName });
        var actor = User.Identity?.Name ?? "admin";
        context.AppendActivity(actor, ActivityLogExtensions.ResolveActorRole(User), ActivityEventType.CategoryCreated,
            $"Kategori eklendi: {trimmedName}", null, null);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!User.HasPermission(Permissions.ManageCategories))
        {
            return Forbid();
        }

        var category = await context.Categories.FindAsync(id);
        if (category is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var isUsed = await context.Products.AnyAsync(p => p.Category == category.Name);
        if (isUsed)
        {
            TempData["CategoryError"] = "Bu kategori urunlerde kullanildigi icin silinemez.";
            return RedirectToAction(nameof(Index));
        }

        var nm = category.Name;
        context.Categories.Remove(category);
        var actor = User.Identity?.Name ?? "admin";
        context.AppendActivity(actor, ActivityLogExtensions.ResolveActorRole(User), ActivityEventType.CategoryDeleted,
            $"Kategori silindi: {nm}", null, null);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
