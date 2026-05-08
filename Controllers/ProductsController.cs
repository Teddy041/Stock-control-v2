using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;
using StockControl.Data;
using StockControl.Models;
using StockControl.ViewModels;

namespace StockControl.Controllers;

public class ProductsController(
    AppDbContext context,
    IWebHostEnvironment environment,
    ILogger<ProductsController> logger) : Controller
{
    [Authorize]
    public async Task<IActionResult> Index()
    {
        if (!User.HasPermission(Permissions.ManageProducts))
        {
            return Forbid();
        }

        var products = await context.Products
            .OrderBy(p => p.Name)
            .ToListAsync();

        return View(products);
    }

    [Authorize]
    public async Task<IActionResult> Catalog(string? q)
    {
        var query = context.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Code.ToLower().Contains(term) ||
                p.Category.ToLower().Contains(term));
        }

        var grouped = await query
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var vm = grouped
            .GroupBy(p => p.Category)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.Query = q;
        ViewBag.RequestMessage = TempData["RequestMessage"];
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> RequestStock(int productId, int requestedAmount = 20, string? note = null)
    {
        var product = await context.Products.FindAsync(productId);
        if (product is null)
        {
            return NotFound();
        }

        if (product.Quantity > 0)
        {
            TempData["RequestMessage"] = "Bu urun stokta oldugu icin talep acilmadi.";
            return RedirectToAction(nameof(Catalog));
        }

        var userName = User.Identity?.Name ?? "Bilinmeyen Kullanici";

        var openRequestExists = await context.StockRequests.AnyAsync(r =>
            !r.IsHandled &&
            r.ProductId == productId &&
            r.RequestedByUserName == userName);

        if (openRequestExists)
        {
            TempData["RequestMessage"] = "Bu urun icin zaten acik bir stok talebiniz var.";
            return RedirectToAction(nameof(Catalog));
        }

        context.StockRequests.Add(new StockRequest
        {
            ProductId = productId,
            RequestedByUserName = userName,
            RequestedAmount = requestedAmount <= 0 ? 1 : requestedAmount,
            Note = note
        });

        context.AppendActivity(
            userName,
            ActivityLogExtensions.ResolveActorRole(User),
            ActivityEventType.StockRequestSubmitted,
            $"Stok talebi: {product.Code} — {product.Name}",
            $"Talep edilen miktar: {(requestedAmount <= 0 ? 1 : requestedAmount)}{(string.IsNullOrWhiteSpace(note) ? "" : $"; Not: {note}")}",
            productId);

        await context.SaveChangesAsync();
        TempData["RequestMessage"] = "Stok talebiniz admin paneline iletildi.";
        return RedirectToAction(nameof(Catalog));
    }

    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        var vm = new ProductDetailsVm
        {
            Product = product,
            Movements = await context.StockMovements
                .Where(m => m.ProductId == id)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync()
        };
        ViewBag.CanUpdateStock = User.HasPermission(Permissions.UpdateStock);

        return View(vm);
    }

    [HttpGet]
    [Authorize]
    public IActionResult Create()
    {
        if (!User.HasPermission(Permissions.ManageProducts))
        {
            return Forbid();
        }

        ViewBag.Categories = context.Categories
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToArray();
        return View(new Product { Category = context.Categories.OrderBy(c => c.Name).Select(c => c.Name).FirstOrDefault() ?? "Diger" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
    {
        if (!User.HasPermission(Permissions.ManageProducts))
        {
            return Forbid();
        }

        product.Code = await GenerateUniqueProductCodeAsync();
        ModelState.Remove(nameof(Product.Code));

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return View(product);
        }

        try
        {
            var uploadResult = await SaveImageAsync(imageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError(string.Empty, uploadResult.ErrorMessage ?? "Gorsel yuklenemedi.");
                ViewBag.Categories = context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
                return View(product);
            }

            product.ImagePath = uploadResult.ImagePath;
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var actorCreate = User.Identity?.Name ?? "admin";
            context.AppendActivity(actorCreate, ActivityLogExtensions.ResolveActorRole(User), ActivityEventType.ProductCreated,
                $"Yeni urun: {product.Code} — {product.Name}",
                $"Kategori: {product.Category}; Stok: {product.Quantity}; Birim fiyat: {product.UnitPrice}",
                product.Id);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
        {
            logger.LogWarning(ex, "Duplicate product code detected while creating product.");
            ModelState.AddModelError(string.Empty, "Urun kodu cakismasi olustu. Lutfen tekrar kaydedin.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Create product failed.");
            ModelState.AddModelError(string.Empty, "Kayit sirasinda beklenmeyen bir hata olustu. Lutfen tekrar deneyin.");
        }

        ViewBag.Categories = context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
        return View(product);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        if (!User.HasPermission(Permissions.ManageProducts))
        {
            return Forbid();
        }

        var product = await context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        ViewBag.Categories = context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Edit(int id, Product product, IFormFile? imageFile)
    {
        if (!User.HasPermission(Permissions.ManageProducts))
        {
            return Forbid();
        }

        if (id != product.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return View(product);
        }

        var existing = await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        // Product code is system-generated and immutable.
        product.Code = existing.Code;

        try
        {
            var uploadResult = await SaveImageAsync(imageFile);
            if (!uploadResult.Success)
            {
                ModelState.AddModelError(string.Empty, uploadResult.ErrorMessage ?? "Gorsel yuklenemedi.");
                ViewBag.Categories = context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
                return View(product);
            }

            product.ImagePath = string.IsNullOrWhiteSpace(uploadResult.ImagePath) ? existing.ImagePath : uploadResult.ImagePath;

            var changes = new List<string>();
            if (existing.Name != product.Name)
            {
                changes.Add($"Ad: {existing.Name} -> {product.Name}");
            }

            if (existing.UnitPrice != product.UnitPrice)
            {
                changes.Add($"Birim fiyat: {existing.UnitPrice} -> {product.UnitPrice}");
            }

            if (existing.Category != product.Category)
            {
                changes.Add($"Kategori: {existing.Category} -> {product.Category}");
            }

            if (existing.Quantity != product.Quantity)
            {
                changes.Add($"Stok: {existing.Quantity} -> {product.Quantity}");
            }

            if (existing.ImagePath != product.ImagePath)
            {
                changes.Add("Urun gorseli guncellendi");
            }

            var onlyQuantityAdjusted =
                existing.Name == product.Name &&
                existing.UnitPrice == product.UnitPrice &&
                existing.Category == product.Category &&
                existing.ImagePath == product.ImagePath &&
                existing.Quantity != product.Quantity;

            var actor = User.Identity?.Name ?? "admin";
            var actorRole = ActivityLogExtensions.ResolveActorRole(User);
            if (onlyQuantityAdjusted)
            {
                context.AppendActivity(actor, actorRole, ActivityEventType.StockDirectEdit,
                    $"Stok (form): {product.Code} — {product.Name}",
                    $"{existing.Quantity} adet -> {product.Quantity} adet",
                    product.Id);
            }
            else if (changes.Count > 0)
            {
                context.AppendActivity(actor, actorRole, ActivityEventType.ProductUpdated,
                    $"Urun guncellendi: {product.Code} — {product.Name}",
                    string.Join("; ", changes),
                    product.Id);
            }

            context.Products.Update(product);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Edit product failed for {ProductId}.", id);
            ModelState.AddModelError(string.Empty, "Guncelleme sirasinda beklenmeyen bir hata olustu. Lutfen tekrar deneyin.");
            ViewBag.Categories = context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return View(product);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        if (!User.HasPermission(Permissions.ManageProducts))
        {
            return Forbid();
        }

        var product = await context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        var actor = User.Identity?.Name ?? "admin";
        context.AppendActivity(actor, ActivityLogExtensions.ResolveActorRole(User), ActivityEventType.ProductDeleted,
            $"Urun silindi: {product.Code} — {product.Name}",
            null,
            product.Id);
        context.Products.Remove(product);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> AddMovement(int productId, MovementType type, int amount, string? description)
    {
        if (!User.HasPermission(Permissions.UpdateStock))
        {
            return Forbid();
        }

        var product = await context.Products.FindAsync(productId);
        if (product is null)
        {
            return NotFound();
        }

        if (amount <= 0)
        {
            TempData["Error"] = "Miktar 0'dan buyuk olmali.";
            return RedirectToAction(nameof(Details), new { id = productId });
        }

        if (type == MovementType.Exit && product.Quantity < amount)
        {
            TempData["Error"] = "Yetersiz stok.";
            return RedirectToAction(nameof(Details), new { id = productId });
        }

        product.Quantity += type == MovementType.Entry ? amount : -amount;

        var movement = new StockMovement
        {
            ProductId = productId,
            Type = type,
            Amount = amount,
            Description = description
        };

        context.StockMovements.Add(movement);
        var moveLabel = type == MovementType.Entry ? "Stok girisi" : "Stok cikisi";
        var actor = User.Identity?.Name ?? "admin";
        context.AppendActivity(actor, ActivityLogExtensions.ResolveActorRole(User), ActivityEventType.StockMovement,
            $"{moveLabel}: {amount} adet — {product.Code} ({product.Name})",
            string.IsNullOrWhiteSpace(description) ? null : $"Not: {description}",
            productId);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = productId });
    }

    private async Task<(bool Success, string? ImagePath, string? ErrorMessage)> SaveImageAsync(IFormFile? imageFile)
    {
        if (imageFile is null || imageFile.Length == 0)
        {
            return (true, null, null);
        }

        const long maxFileSize = 2 * 1024 * 1024;
        if (imageFile.Length > maxFileSize)
        {
            return (false, null, "Gorsel boyutu en fazla 2 MB olabilir.");
        }

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string> { ".png", ".jpg", ".jpeg", ".webp" };
        if (!allowedExtensions.Contains(extension))
        {
            return (false, null, "Sadece .png, .jpg, .jpeg veya .webp dosyalari yuklenebilir.");
        }

        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        var uploadsPath = Path.Combine(webRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsPath, fileName);

        try
        {
            await using var stream = System.IO.File.Create(filePath);
            await imageFile.CopyToAsync(stream);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Physical file write failed for upload.");
            return (false, null, "Gorsel dosyasi diske kaydedilemedi.");
        }

        return (true, $"/uploads/{fileName}", null);
    }

    private async Task<string> GenerateUniqueProductCodeAsync()
    {
        var codes = await context.Products
            .Select(p => p.Code)
            .ToListAsync();

        var maxNumber = 0;
        var pattern = new Regex(@"^PRD-(\d+)$", RegexOptions.IgnoreCase);
        foreach (var code in codes)
        {
            var match = pattern.Match(code);
            if (!match.Success)
            {
                continue;
            }

            if (int.TryParse(match.Groups[1].Value, out var number) && number > maxNumber)
            {
                maxNumber = number;
            }
        }

        return $"PRD-{(maxNumber + 1):D4}";
    }
}
