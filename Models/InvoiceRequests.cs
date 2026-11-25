namespace GhoghnoosAcc.Models;

public sealed record InvoiceCreateRequest(
    string? Number,
    string? Kind,
    DateTime InvoiceDate,
    int PartyId,
    string? Notes,
    List<InvoiceCreateLineRequest>? Lines);

public sealed record InvoiceCreateLineRequest(
    string? Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate);

public sealed record PaymentCreateRequest(
    string? Number,
    string? Kind,
    DateTime PaymentDate,
    int PartyId,
    int CashAccountId,
    int? InvoiceId,
    decimal Amount,
    string? Notes);
