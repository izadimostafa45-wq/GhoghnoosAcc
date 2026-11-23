using System.ComponentModel.DataAnnotations;

namespace GhoghnoosAcc.Models;

/// <summary>
/// Connects an authenticated user to a business workspace.
/// Authorization must verify an active membership server-side.
/// </summary>
public sealed class TenantMembership
{
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, StringLength(40)]
    public string Role { get; set; } = TenantRoles.ReadOnly;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class TenantRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Accountant = "Accountant";
    public const string Sales = "Sales";
    public const string Service = "Service";
    public const string ReadOnly = "ReadOnly";
}
