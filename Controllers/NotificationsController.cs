using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockControl.Data;
using StockControl.Models;
using StockControl.ViewModels;

namespace StockControl.Controllers;

[Authorize]
public class NotificationsController(AppDbContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.HasPermission(Permissions.HandleStockRequests))
        {
            return Forbid();
        }

        var vm = new AdminNotificationsVm
        {
            LowStockProducts = await context.Products
                .Where(p => p.Quantity < 50)
                .OrderBy(p => p.Quantity)
                .ToListAsync(),
            OpenStockRequests = await context.StockRequests
                .Include(r => r.Product)
                .Where(r => !r.IsHandled)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRequestHandled(int id)
    {
        if (!User.HasPermission(Permissions.HandleStockRequests))
        {
            return Forbid();
        }

        var request = await context.StockRequests
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request is null)
        {
            return RedirectToAction(nameof(Index));
        }

        request.IsHandled = true;
        var admin = User.Identity?.Name ?? "admin";
        var productLabel = request.Product is not null
            ? $"{request.Product.Code} — {request.Product.Name}"
            : $"Urun #{request.ProductId}";
        context.AppendActivity(
            admin,
            "Admin",
            ActivityEventType.StockRequestHandled,
            $"Stok talebi kapatildi: {productLabel}",
            $"Talep eden: {request.RequestedByUserName}, Miktar: {request.RequestedAmount}",
            request.ProductId);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
