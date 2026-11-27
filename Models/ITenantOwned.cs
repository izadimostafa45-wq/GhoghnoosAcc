namespace GhoghnoosAcc.Models;

/// <summary>Marks persisted business records that belong to exactly one tenant.</summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
