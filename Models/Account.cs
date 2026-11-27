using System.ComponentModel.DataAnnotations;

namespace GhoghnoosAcc.Models;

public class Account : ITenantOwned
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    [Required, StringLength(30)] public string Code { get; set; } = "";
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(30)] public string Type { get; set; } = "General";
    public int? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }
    public bool IsActive { get; set; } = true;
}