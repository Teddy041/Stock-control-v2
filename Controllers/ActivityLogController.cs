using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockControl.Data;

namespace StockControl.Controllers;

[Authorize]
public class ActivityLogController(AppDbContext context) : Controller
{
    private const int PageSize = 40;

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        if (!User.HasPermission(Permissions.ViewLogs))
        {
            return Forbid();
        }

        page = Math.Max(1, page);
        var query = context.ActivityLogs.AsNoTracking().OrderByDescending(a => a.CreatedAt);
        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        ViewBag.TotalCount = total;
        return View(items);
    }
}
