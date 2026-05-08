using System.ComponentModel.DataAnnotations;

namespace StockControl.ViewModels;

public class RegisterVm
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [Display(Name = "Kullanici Adi")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6)]
    [Display(Name = "Sifre")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Sifre Tekrar")]
    [Compare(nameof(Password), ErrorMessage = "Sifreler eslesmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
