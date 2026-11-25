using System.Security.Claims;
using GhoghnoosAcc.Models;
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
    IConfiguration configuration)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var configuredEmail = configuration["PlatformAdmin:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredEmail))
            configuredEmail = PlatformAdminAuthorization.DefaultEmail;

        if (string.Equals(user.Email, configuredEmail, StringComparison.OrdinalIgnoreCase)
            && !identity.HasClaim(PlatformAdminAuthorization.ClaimType, "true"))
        {
            identity.AddClaim(new Claim(PlatformAdminAuthorization.ClaimType, "true"));
        }

        return identity;
    }
}
