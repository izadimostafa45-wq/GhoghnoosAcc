using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GhoghnoosAcc.Models;

public static class InvoiceKinds
{
    public const string Sales = "Sales";
    public const string Purchase = "Purchase";
}

public static class InvoiceStatuses
{
    public const string Draft = "Draft";
    public const string Posted = "Posted";
}

public sealed class Invoice : ITenantOwned
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    [Required, StringLength(40)] public string Number { get; set; } = "";
    [Required, StringLength(20)] public string Kind { get; set; } = InvoiceKinds.Sales;
    [Required, StringLength(20)] public string Status { get; set; } = InvoiceStatuses.Draft;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;
    public int PartyId { get; set; }
    public Party Party { get; set; } = null!;
    public int? CounterpartyAccountId { get; set; }
    public Account? CounterpartyAccount { get; set; }
    public int? OperatingAccountId { get; set; }
    public Account? OperatingAccount { get; set; }
    public int? TaxAccountId { get; set; }
    public Account? TaxAccount { get; set; }
    public int? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal Subtotal { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal TaxTotal { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<InvoiceLine> Lines { get; set; } = [];
}

public sealed class InvoiceLine : ITenantOwned
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    [Required, StringLength(200)] public string Description { get; set; } = "";
    [Column(TypeName = "numeric(18,3)")] public decimal Quantity { get; set; } = 1m;
    [Column(TypeName = "numeric(18,2)")] public decimal UnitPrice { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal Discount { get; set; }
    [Column(TypeName = "numeric(5,2)")] public decimal TaxRate { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal LineSubtotal { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal LineTax { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal LineTotal { get; set; }
}

public static class PaymentKinds
{
    public const string Receipt = "Receipt";
    public const string Payment = "Payment";
}

public sealed class PaymentTransaction : ITenantOwned
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    [Required, StringLength(40)] public string Number { get; set; } = "";
    [Required, StringLength(20)] public string Kind { get; set; } = PaymentKinds.Receipt;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;
    public int PartyId { get; set; }
    public Party Party { get; set; } = null!;
    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int CashAccountId { get; set; }
    public Account CashAccount { get; set; } = null!;
    public int? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    [Column(TypeName = "numeric(18,2)")] public decimal Amount { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
