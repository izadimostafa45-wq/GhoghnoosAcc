using System.ComponentModel.DataAnnotations;

namespace GhoghnoosAcc.Models;

/// <summary>
/// A business workspace. Business-owned data must be scoped to this tenant.
/// This model is not enabled in persistence until a reviewed EF migration is added.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Status { get; set; } = TenantStatuses.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();
}

public static class TenantStatuses
{
    public const string Active = "Active";
    public const string Suspended = "Suspended";
}
