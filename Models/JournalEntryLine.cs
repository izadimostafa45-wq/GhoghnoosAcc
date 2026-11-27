using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GhoghnoosAcc.Models;

public class JournalEntryLine : ITenantOwned
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public int? PartyId { get; set; }
    public Party? Party { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal Debit { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal Credit { get; set; }
    [StringLength(300)] public string? Description { get; set; }
}