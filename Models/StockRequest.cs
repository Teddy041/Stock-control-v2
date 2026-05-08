using System.ComponentModel.DataAnnotations;

namespace StockControl.Models;

public class StockRequest
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string RequestedByUserName { get; set; } = string.Empty;

    [Range(1, 999999)]
    public int RequestedAmount { get; set; } = 1;

    [StringLength(200)]
    public string? Note { get; set; }

    public bool IsHandled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
