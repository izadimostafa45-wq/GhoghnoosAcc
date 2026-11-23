using Microsoft.AspNetCore.Identity;

namespace GhoghnoosAcc.Models;

/// <summary>
/// Authentication identity. Password storage and verification are delegated to ASP.NET Core Identity.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    public ICollection<TenantMembership> TenantMemberships { get; set; } = new List<TenantMembership>();
}
