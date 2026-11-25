using System.Security.Claims;
using GhoghnoosAcc.Models;
using Microsoft.AspNetCore.Authorization;

namespace GhoghnoosAcc.Services;

/// <summary>
/// Central claim-based authorization rules for the currently selected workspace.
/// Tenant and role claims are issued only after server-side membership validation.
/// </summary>
public static class TenantRoleAuthorization
{
    public const string RoleClaimType = "ghoghnoos:tenant_role";

    public static readonly string[] ReadRoles =
    [
        TenantRoles.Owner,
        TenantRoles.Admin,
        TenantRoles.Accountant,
        TenantRoles.Sales,
        TenantRoles.Service,
        TenantRoles.ReadOnly
    ];

    public static readonly string[] WriteRoles =
    [
        TenantRoles.Owner,
        TenantRoles.Admin,
        TenantRoles.Accountant
    ];

    public static bool HasValidTenantAndRole(ClaimsPrincipal principal)
    {
        var tenantValue = principal.FindFirst(TenantContext.TenantIdClaimType)?.Value;
        var role = principal.FindFirst(RoleClaimType)?.Value;

        return Guid.TryParse(tenantValue, out var tenantId)
            && tenantId != Guid.Empty
            && !string.IsNullOrWhiteSpace(role)
            && ReadRoles.Contains(role, StringComparer.Ordinal);
    }

    public static bool HasAnyRole(ClaimsPrincipal principal, IEnumerable<string> allowedRoles) =>
        HasValidTenantAndRole(principal)
        && allowedRoles.Contains(
            principal.FindFirst(RoleClaimType)!.Value,
            StringComparer.Ordinal);
}
