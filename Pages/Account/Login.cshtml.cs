using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GhoghnoosAcc.Data;
using GhoghnoosAcc.Models;
using GhoghnoosAcc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GhoghnoosAcc.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    AppDbContext db) : PageModel
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
            : await signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: true);

        if (result.Succeeded && user is not null)
        {
            // Do not issue an authenticated cookie until a valid active workspace
            // membership has been resolved from the database.
            var memberships = await db.TenantMemberships
                .AsNoTracking()
                .Where(m => m.UserId == user.Id
                    && m.IsActive
                    && m.Tenant != null
                    && m.Tenant.Status == TenantStatuses.Active
                    && TenantRoleAuthorization.ReadRoles.Contains(m.Role))
                .Select(m => new { m.TenantId, m.Role })
                .Take(2)
                .ToListAsync();

            if (memberships.Count == 0)
            {
                ModelState.AddModelError(string.Empty,
                    "ورود انجام نشد؛ برای این حساب، عضویت فعال در هیچ شرکت ثبت نشده است.");
                return Page();
            }

            if (memberships.Count > 1)
            {
                // Establish only the identity first. The accounting API still rejects
                // this cookie because it has no tenant claim until selection is validated.
                await signInManager.SignInAsync(user, Input.RememberMe);
                return RedirectToPage("/Account/SelectTenant", new { returnUrl = ReturnUrl, rememberMe = Input.RememberMe });
            }

            var membership = memberships[0];
            await signInManager.SignInWithClaimsAsync(user, Input.RememberMe, new[]
            {
                new Claim(TenantContext.TenantIdClaimType, membership.TenantId.ToString("D")),
                new Claim(TenantRoleAuthorization.RoleClaimType, membership.Role)
            });

            return LocalRedirect(ReturnUrl);
        }

        if (result.IsLockedOut)
            ModelState.AddModelError(string.Empty, "به دلیل تلاش‌های ناموفق، حساب موقتاً قفل شده است.");
        else
            ModelState.AddModelError(string.Empty, "ایمیل یا گذرواژه صحیح نیست.");

        return Page();
    }
}
