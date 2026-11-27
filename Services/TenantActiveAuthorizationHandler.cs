using System.Security.Claims;
using GhoghnoosAcc.Data;
using GhoghnoosAcc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GhoghnoosAcc.Services;

/// <summary>
/// Revalidates the tenant and membership on every tenant-protected request so
/// suspended companies and revoked memberships lose access without waiting for re-login.
/// </summary>
public sealed class TenantActiveRequirement : IAuthorizationRequirement { }

public sealed class TenantActiveAuthorizationHandler(AppDbContext db)
    : AuthorizationHandler<TenantActiveRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantActiveRequirement requirement)
    {
        var principal = context.User;
        if (!TenantRoleAuthorization.HasValidTenantAndRole(principal))
            return;

        if (!Guid.TryParse(principal.FindFirst(TenantContext.TenantIdClaimType)?.Value, out var tenantId))
            return;

        var tenantIsActive = await db.Tenants.AsNoTracking()
            .AnyAsync(x => x.Id == tenantId && x.Status == TenantStatuses.Active);
        if (!tenantIsActive)
            return;

        // Platform administrators can select any active company from the dedicated
        // admin panel. Ordinary users must still have a matching active membership.
        if (principal.HasClaim(PlatformAdminAuthorization.ClaimType, "true"))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = principal.FindFirst(TenantRoleAuthorization.RoleClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
            return;

        var hasMembership = await db.TenantMemberships.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.TenantId == tenantId &&
                x.IsActive && x.Role == role && x.Tenant!.Status == TenantStatuses.Active);
        if (hasMembership)
            context.Succeed(requirement);
    }
}
