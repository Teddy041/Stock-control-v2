using System.ComponentModel.DataAnnotations;

namespace StockControl.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    public string Category { get; set; } = "Genel";

    [Range(0, 999999)]
    public decimal UnitPrice { get; set; }

    [Range(0, 999999)]
    public int Quantity { get; set; }

    [StringLength(250)]
    public string? ImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
