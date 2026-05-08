using System.ComponentModel.DataAnnotations;

namespace StockControl.Models;

public class Category
{
    public int Id { get; set; }

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;
}
