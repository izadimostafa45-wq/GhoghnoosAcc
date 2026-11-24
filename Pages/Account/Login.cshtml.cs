using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GhoghnoosAcc.Models;

namespace GhoghnoosAcc.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public sealed class LoginInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
    }

    [BindProperty]
    public string ReturnUrl { get; set; } = "/";

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        if (!ModelState.IsValid)
            return Page();

        var user = await userManager.FindByEmailAsync(Input.Email.Trim());
        var result = user is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signInManager.PasswordSignInAsync(user.UserName!, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(ReturnUrl);

        if (result.IsLockedOut)
            ModelState.AddModelError(string.Empty, "به دلیل تلاش‌های ناموفق، حساب موقتاً قفل شده است.");
        else
            ModelState.AddModelError(string.Empty, "ایمیل یا گذرواژه صحیح نیست.");

        return Page();
    }
}
