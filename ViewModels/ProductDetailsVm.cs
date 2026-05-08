using StockControl.Models;

namespace StockControl.ViewModels;

public class ProductDetailsVm
{
    public Product Product { get; set; } = null!;
    public List<StockMovement> Movements { get; set; } = new();
}
