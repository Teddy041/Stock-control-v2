using System.ComponentModel.DataAnnotations;

namespace StockControl.Models;

public enum ActivityEventType
{
    OrderCheckout = 1,
    StockMovement = 2,
    ProductCreated = 3,
    ProductUpdated = 4,
    ProductDeleted = 5,
    StockRequestSubmitted = 6,
    StockRequestHandled = 7,
    CategoryCreated = 8,
    CategoryDeleted = 9,
    StockDirectEdit = 10,
    UserCreated = 11,
    UserPermissionsUpdated = 12,
    UserDeleted = 13,
    UserPasswordReset = 14
}

public class ActivityLog
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(64)]
    public string? ActorRole { get; set; }

    public ActivityEventType EventType { get; set; }

    [Required]
    [StringLength(500)]
    public string Summary { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Details { get; set; }

    public int? ProductId { get; set; }
}
