using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StockControl.ViewModels;

namespace StockControl.Controllers;

public class AccountController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["HideNavbar"] = true;
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewData["HideNavbar"] = true;
            return View(vm);
        }

        var result = await signInManager.PasswordSignInAsync(vm.UserName, vm.Password, true, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ViewData["HideNavbar"] = true;
            ModelState.AddModelError(string.Empty, "Kullanici adi veya sifre hatali.");
            return View(vm);
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Catalog", "Products");
    }

    [HttpGet]
    public IActionResult Register()
    {
        ViewData["HideNavbar"] = true;
        return View(new RegisterVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        ViewData["HideNavbar"] = true;
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var userExists = await userManager.FindByNameAsync(vm.UserName);
        if (userExists is not null)
        {
            ModelState.AddModelError(nameof(vm.UserName), "Bu kullanici adi zaten kullaniliyor.");
            return View(vm);
        }

        var user = new IdentityUser { UserName = vm.UserName };
        var createResult = await userManager.CreateAsync(user, vm.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(vm);
        }

        if (await userManager.IsInRoleAsync(user, "User") is false)
        {
            await userManager.AddToRoleAsync(user, "User");
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return RedirectToAction("Catalog", "Products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
