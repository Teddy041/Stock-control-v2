using StockControl.Models;

namespace StockControl.ViewModels;

public class AdminNotificationsVm
{
    public List<Product> LowStockProducts { get; set; } = new();
    public List<StockRequest> OpenStockRequests { get; set; } = new();
}
