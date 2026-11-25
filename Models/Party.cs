using System.ComponentModel.DataAnnotations;

namespace GhoghnoosAcc.Models;

public class Party : ITenantOwned
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}