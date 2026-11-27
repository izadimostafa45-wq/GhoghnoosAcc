using System.Security.Claims;
using GhoghnoosAcc.Models;
using GhoghnoosAcc.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GhoghnoosAcc.Services;

/// <summary>
/// Rehydrates the platform-admin claim whenever ASP.NET Identity rebuilds a user principal.
/// The designation is based on the configured account email, not on tenant membership.
/// </summary>
public sealed class PlatformAdminClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options,
    IConfiguration configuration,
    AppDbContext db)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var configuredEmail = configuration["PlatformAdmin:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredEmail))
            configuredEmail = PlatformAdminAuthorization.DefaultEmail;

        var isPlatformAdmin = string.Equals(user.Email, configuredEmail, StringComparison.OrdinalIgnoreCase);
        if (isPlatformAdmin)
        {
            if (!identity.HasClaim(PlatformAdminAuthorization.ClaimType, "true"))
                identity.AddClaim(new Claim(PlatformAdminAuthorization.ClaimType, "true"));

            // Platform admins select a workspace explicitly; never silently attach one.
            return identity;
        }

        // Automatically activate a tenant only when the user has exactly one active
        // membership in an active company. Multiple memberships require an explicit
        // workspace-selection flow rather than choosing one arbitrarily.
        var memberships = await db.TenantMemberships.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.IsActive && x.Tenant != null && x.Tenant.Status == TenantStatuses.Active)
            .Select(x => new { x.TenantId, x.Role })
            .Take(2)
            .ToListAsync();
        if (memberships.Count == 1 && TenantRoleAuthorization.IsKnownRole(memberships[0].Role))
        {
            identity.AddClaim(new Claim(TenantContext.TenantIdClaimType, memberships[0].TenantId.ToString()));
            identity.AddClaim(new Claim(TenantRoleAuthorization.RoleClaimType, memberships[0].Role));
        }

        return identity;
    }
}
