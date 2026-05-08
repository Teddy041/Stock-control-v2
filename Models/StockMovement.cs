using System.ComponentModel.DataAnnotations;

namespace StockControl.Models;

public enum MovementType
{
    Entry = 1,
    Exit = 2
}

public class StockMovement
{
    public int Id { get; set; }

    [Required]
    public MovementType Type { get; set; }

    [Range(1, 999999)]
    public int Amount { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
