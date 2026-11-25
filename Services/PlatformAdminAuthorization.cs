namespace GhoghnoosAcc.Services;

/// <summary>
/// Marks an authenticated platform-level administrator. This role is separate from
/// tenant membership and must never be treated as a tenant/accounting-data claim.
/// </summary>
public static class PlatformAdminAuthorization
{
    public const string ClaimType = "ghoghnoos:platform_admin";
    public const string DefaultEmail = "ghoghnoos.office@gmil.com";
}
