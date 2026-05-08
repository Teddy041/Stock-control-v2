using System.Security.Claims;
using StockControl.Models;

namespace StockControl.Data;

public static class ActivityLogExtensions
{
    public static string? ResolveActorRole(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin"))
        {
            return "Admin";
        }

        if (user.IsInRole("User"))
        {
            return "User";
        }

        return null;
    }

    public static void AppendActivity(
        this AppDbContext db,
        string userName,
        string? actorRole,
        ActivityEventType eventType,
        string summary,
        string? details = null,
        int? productId = null)
    {
        db.ActivityLogs.Add(new ActivityLog
        {
            CreatedAt = DateTime.UtcNow,
            UserName = userName,
            ActorRole = actorRole,
            EventType = eventType,
            Summary = summary,
            Details = details,
            ProductId = productId
        });
    }

    public static string ActivityEventDisplayTurkish(ActivityEventType t) => t switch
    {
        ActivityEventType.OrderCheckout => "Siparis",
        ActivityEventType.StockMovement => "Stok hareketi (admin)",
        ActivityEventType.ProductCreated => "Urun eklendi",
        ActivityEventType.ProductUpdated => "Urun guncellendi",
        ActivityEventType.ProductDeleted => "Urun silindi",
        ActivityEventType.StockRequestSubmitted => "Stok talebi",
        ActivityEventType.StockRequestHandled => "Talep kapatildi",
        ActivityEventType.CategoryCreated => "Kategori eklendi",
        ActivityEventType.CategoryDeleted => "Kategori silindi",
        ActivityEventType.StockDirectEdit => "Stok (formdan)",
        ActivityEventType.UserCreated => "Uye olusturuldu",
        ActivityEventType.UserPermissionsUpdated => "Uye yetkileri",
        ActivityEventType.UserDeleted => "Uye silindi",
        ActivityEventType.UserPasswordReset => "Sifre sifirlandi",
        _ => t.ToString()
    };
}
