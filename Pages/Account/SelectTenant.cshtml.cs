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

[Authorize]
public sealed class SelectTenantModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext db) : PageModel
{
    public sealed class TenantOption
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
    }

    public IReadOnlyList<TenantOption> Tenants { get; private set; } = Array.Empty<TenantOption>();

    [BindProperty]
    public Guid TenantId { get; set; }

    [BindProperty]
    public string ReturnUrl { get; set; } = "/";

    [BindProperty]
    public bool RememberMe { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null, bool rememberMe = false)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        RememberMe = rememberMe;
        await LoadTenantsAsync();
        if (Tenants.Count == 0)
        {
            await signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var membership = await db.TenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == user.Id
                && m.TenantId == TenantId
                && m.IsActive
                && m.Tenant != null
                && m.Tenant.Status == TenantStatuses.Active)
            .Select(m => new { m.TenantId, m.Role })
            .SingleOrDefaultAsync();

        // Never trust the posted tenant ID: it must match an active membership.
        if (membership is null || string.IsNullOrWhiteSpace(membership.Role))
        {
            ModelState.AddModelError(string.Empty, "شرکت انتخاب‌شده معتبر نیست یا دسترسی فعال ندارید.");
            await LoadTenantsAsync();
            return Page();
        }

        await signInManager.SignInWithClaimsAsync(user, RememberMe, new[]
        {
            new Claim(TenantContext.TenantIdClaimType, membership.TenantId.ToString("D")),
            new Claim(TenantRoleAuthorization.RoleClaimType, membership.Role)
        });

        return LocalRedirect(ReturnUrl);
    }

    private async Task LoadTenantsAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            Tenants = Array.Empty<TenantOption>();
            return;
        }

        Tenants = await db.TenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == user.Id
                && m.IsActive
                && m.Tenant != null
                && m.Tenant.Status == TenantStatuses.Active)
            .OrderBy(m => m.Tenant!.Name)
            .Select(m => new TenantOption
            {
                Id = m.TenantId,
                Name = m.Tenant!.Name,
                Role = m.Role
            })
            .ToListAsync();
    }
}
