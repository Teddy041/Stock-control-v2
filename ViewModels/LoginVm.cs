using System.ComponentModel.DataAnnotations;

namespace StockControl.ViewModels;

public class LoginVm
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
