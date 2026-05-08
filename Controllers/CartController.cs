using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockControl.Data;
using StockControl.Models;
using StockControl.ViewModels;

namespace StockControl.Controllers;

[Authorize(Roles = "User")]
public class CartController(AppDbContext context) : Controller
{
    private const string CartSessionKey = "cart-items";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = new CartVm
        {
            Items = await BuildCartItemsAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        var product = await context.Products.FindAsync(productId);
        if (product is null)
        {
            return NotFound();
        }

        if (product.Quantity <= 0)
        {
            TempData["CartError"] = "Bu urun stokta yok.";
            return RedirectToAction("Catalog", "Products");
        }

        quantity = Math.Max(1, quantity);

        var cart = GetCartMap();
        cart.TryGetValue(productId, out var currentQuantity);
        var requestedTotal = currentQuantity + quantity;

        if (requestedTotal > product.Quantity)
        {
            TempData["CartError"] = "Sepete eklemek icin yeterli stok yok.";
            return RedirectToAction("Catalog", "Products");
        }

        cart[productId] = requestedTotal;
        SaveCartMap(cart);

        TempData["CartSuccess"] = "Urun sepete eklendi.";
        return RedirectToAction("Catalog", "Products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update(int productId, int quantity)
    {
        var cart = GetCartMap();
        if (!cart.ContainsKey(productId))
        {
            return RedirectToAction(nameof(Index));
        }

        if (quantity <= 0)
        {
            cart.Remove(productId);
        }
        else
        {
            cart[productId] = quantity;
        }

        SaveCartMap(cart);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int productId)
    {
        var cart = GetCartMap();
        if (cart.Remove(productId))
        {
            SaveCartMap(cart);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout()
    {
        var cart = GetCartMap();
        if (cart.Count == 0)
        {
            TempData["CartError"] = "Sepetiniz bos.";
            return RedirectToAction(nameof(Index));
        }

        var productIds = cart.Keys.ToList();
        var products = await context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        foreach (var product in products)
        {
            var requested = cart[product.Id];
            if (product.Quantity < requested)
            {
                TempData["CartError"] = $"{product.Name} icin yeterli stok yok.";
                return RedirectToAction(nameof(Index));
            }
        }

        foreach (var product in products)
        {
            product.Quantity -= cart[product.Id];
        }

        var userName = User.Identity?.Name ?? "?";
        var role = ActivityLogExtensions.ResolveActorRole(User);
        var summary = $"Siparis tamamlandi — {products.Count} kalem";
        var detailLines = products
            .Select(p => $"{p.Code}  {p.Name}  x{cart[p.Id]} adet  (kalan stok: {p.Quantity})")
            .ToList();
        context.AppendActivity(userName, role, ActivityEventType.OrderCheckout, summary, string.Join("\n", detailLines), null);

        await context.SaveChangesAsync();
        SaveCartMap(new Dictionary<int, int>());

        TempData["CartSuccess"] = "Satin alma tamamlandi. Stoklar guncellendi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<CartItemVm>> BuildCartItemsAsync()
    {
        var cart = GetCartMap();
        if (cart.Count == 0)
        {
            return new List<CartItemVm>();
        }

        var productIds = cart.Keys.ToList();
        var products = await context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        return products
            .Select(p => new CartItemVm
            {
                ProductId = p.Id,
                ProductName = p.Name,
                UnitPrice = p.UnitPrice,
                Quantity = cart[p.Id],
                AvailableStock = p.Quantity
            })
            .OrderBy(i => i.ProductName)
            .ToList();
    }

    private Dictionary<int, int> GetCartMap()
    {
        var json = HttpContext.Session.GetString(CartSessionKey);
        return string.IsNullOrWhiteSpace(json)
            ? new Dictionary<int, int>()
            : JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
    }

    private void SaveCartMap(Dictionary<int, int> cart)
    {
        var json = JsonSerializer.Serialize(cart);
        HttpContext.Session.SetString(CartSessionKey, json);
    }
}
