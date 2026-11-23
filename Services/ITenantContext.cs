using System.Security.Claims;

namespace GhoghnoosAcc.Services;

/// <summary>
/// Resolves the active tenant exclusively from server-issued authenticated claims.
/// Never resolve a tenant from query strings, route values, or request bodies.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    bool HasTenant { get; }
}

public sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string TenantIdClaimType = "ghoghnoos:tenant_id";

    public Guid? TenantId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            var value = user.FindFirstValue(TenantIdClaimType);
            return Guid.TryParse(value, out var tenantId) && tenantId != Guid.Empty
                ? tenantId
                : null;
        }
    }

    public bool HasTenant => TenantId.HasValue;
}
