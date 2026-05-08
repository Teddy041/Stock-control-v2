namespace StockControl.ViewModels;

public class CartVm
{
    public List<CartItemVm> Items { get; set; } = new();
    public decimal GrandTotal => Items.Sum(i => i.LineTotal);
}
