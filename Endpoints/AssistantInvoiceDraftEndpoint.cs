using GhoghnoosAcc.Data;
using GhoghnoosAcc.Models;
using GhoghnoosAcc.Services;
using Microsoft.EntityFrameworkCore;

namespace GhoghnoosAcc.Endpoints;

/// <summary>
/// Creates draft invoices from structured assistant output. This endpoint never posts
/// an invoice or creates a journal entry; final posting remains a separate user action.
/// </summary>
public static class AssistantInvoiceDraftEndpoint
{
    public static void MapAssistantInvoiceDraftEndpoint(this RouteGroupBuilder accountingApi)
    {
        accountingApi.MapPost("/assistant/invoice-drafts", async Task<IResult> (
            AssistantInvoiceDraftRequest request,
            AppDbContext db,
            ITenantContext tenantContext) =>
        {
            if (tenantContext.TenantId is not Guid tenantId)
                return Results.Forbid();

            var kind = request.Kind?.Trim();
            var partyName = request.PartyName?.Trim();
            if (kind is not (InvoiceKinds.Sales or InvoiceKinds.Purchase))
                return Results.BadRequest(new { message = "نوع فاکتور باید Sales یا Purchase باشد." });
            if (string.IsNullOrWhiteSpace(partyName) || partyName.Length > 200)
                return Results.BadRequest(new { message = "نام مشتری یا تأمین‌کننده الزامی است." });
            if (request.Lines is null || request.Lines.Count is < 1 or > 100)
                return Results.BadRequest(new { message = "فاکتور باید بین ۱ تا ۱۰۰ ردیف داشته باشد." });
            if ((request.Notes?.Length ?? 0) > 500)
                return Results.BadRequest(new { message = "توضیحات حداکثر ۵۰۰ نویسه می‌تواند باشد." });
            if (request.Lines.Any(line =>
                string.IsNullOrWhiteSpace(line.Description) ||
                line.Description.Trim().Length > 200 ||
                line.Quantity <= 0 ||
                line.Quantity > 1_000_000_000m ||
                InvoiceDecimalPrecision.HasScaleAbove(line.Quantity, 3) ||
                line.UnitPrice < 0 ||
                line.UnitPrice > 1_000_000_000_000m ||
                InvoiceDecimalPrecision.HasScaleAbove(line.UnitPrice, 2) ||
                line.Discount < 0 ||
                InvoiceDecimalPrecision.HasScaleAbove(line.Discount, 2) ||
                line.TaxRate < 0 || line.TaxRate > 100 ||
                InvoiceDecimalPrecision.HasScaleAbove(line.TaxRate, 2)))
                return Results.BadRequest(new { message = "تعداد، قیمت، تخفیف یا مالیات یکی از ردیف‌ها معتبر نیست." });

            var matchingParties = await db.Parties.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive && p.Name.ToLower() == partyName.ToLower())
                .OrderBy(p => p.Id)
                .Take(2)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            if (matchingParties.Count == 0)
                return Results.NotFound(new
                {
                    code = "party_not_found",
                    message = "مشتری یا تأمین‌کننده‌ای با این نام پیدا نشد. ابتدا شخص را در نرم‌افزار ثبت کنید یا نام دقیق را مشخص کنید.",
                    partyName
                });
            if (matchingParties.Count > 1)
                return Results.Conflict(new
                {
                    code = "party_name_ambiguous",
                    message = "چند طرف حساب با این نام وجود دارد؛ برای جلوگیری از ثبت اشتباه، انتخاب شناسه طرف حساب لازم است.",
                    candidates = matchingParties
                });

            var lines = request.Lines.Select(item =>
            {
                var gross = decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero);
                if (item.Discount > gross || !InvoiceDecimalPrecision.FitsNumeric18_2(gross))
                    return null;

                var subtotal = gross - item.Discount;
                var tax = decimal.Round(subtotal * item.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
                var total = subtotal + tax;
                if (!InvoiceDecimalPrecision.FitsNumeric18_2(subtotal) ||
                    !InvoiceDecimalPrecision.FitsNumeric18_2(tax) ||
                    !InvoiceDecimalPrecision.FitsNumeric18_2(total))
                    return null;

                return new InvoiceLine
                {
                    TenantId = tenantId,
                    Description = item.Description!.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Discount = item.Discount,
                    TaxRate = item.TaxRate,
                    LineSubtotal = subtotal,
                    LineTax = tax,
                    LineTotal = total
                };
            }).ToList();

            if (lines.Any(line => line is null))
                return Results.BadRequest(new
                {
                    message = "تخفیف نباید از مبلغ ردیف بیشتر باشد و مبلغ‌های محاسبه‌شده باید در محدوده مجاز دیتابیس باشند."
                });

            var invoiceLines = lines.Select(line => line!).ToList();
            var invoiceSubtotal = invoiceLines.Sum(line => line.LineSubtotal);
            var invoiceTaxTotal = invoiceLines.Sum(line => line.LineTax);
            var invoiceTotal = invoiceLines.Sum(line => line.LineTotal);
            if (!InvoiceDecimalPrecision.FitsNumeric18_2(invoiceSubtotal) ||
                !InvoiceDecimalPrecision.FitsNumeric18_2(invoiceTaxTotal) ||
                !InvoiceDecimalPrecision.FitsNumeric18_2(invoiceTotal))
                return Results.BadRequest(new
                {
                    message = "مجموع فاکتور از سقف مبلغ قابل ذخیره در دیتابیس بیشتر است؛ تعداد یا مبلغ ردیف‌ها را کاهش دهید."
                });

            var invoiceDate = (request.InvoiceDate ?? DateTime.UtcNow).Date;
            var number = string.IsNullOrWhiteSpace(request.Number)
                ? $"AI-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                : request.Number.Trim();
            if (number.Length > 40)
                return Results.BadRequest(new { message = "شماره فاکتور حداکثر ۴۰ نویسه می‌تواند باشد." });
            if (await db.Invoices.AnyAsync(i => i.TenantId == tenantId && i.Number == number))
                return Results.Conflict(new { code = "invoice_number_exists", message = "شماره فاکتور تکراری است؛ شماره دیگری تعیین کنید.", number });

            var invoice = new Invoice
            {
                TenantId = tenantId,
                Number = number,
                Kind = kind,
                Status = InvoiceStatuses.Draft,
                InvoiceDate = DateTime.SpecifyKind(invoiceDate, DateTimeKind.Utc),
                PartyId = matchingParties[0].Id,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Lines = invoiceLines,
                Subtotal = invoiceSubtotal,
                TaxTotal = invoiceTaxTotal,
                Total = invoiceTotal
            };

            db.Invoices.Add(invoice);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                ex.GetBaseException() is Npgsql.PostgresException postgresException &&
                postgresException.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName == "IX_Invoices_TenantId_Number")
            {
                // The pre-check above improves the normal response, but concurrent requests can
                // still race. The database unique index is authoritative.
                return Results.Conflict(new
                {
                    code = "invoice_number_exists",
                    message = "شماره فاکتور تکراری است؛ شماره دیگری تعیین کنید.",
                    number
                });
            }

            return Results.Created($"/api/accounting/invoices/{invoice.Id}", new
            {
                invoice.Id,
                invoice.Number,
                invoice.Kind,
                invoice.Status,
                PartyId = matchingParties[0].Id,
                PartyName = matchingParties[0].Name,
                invoice.InvoiceDate,
                Lines = invoiceLines.Select(line => new
                {
                    line.Description, line.Quantity, line.UnitPrice, line.Discount,
                    line.TaxRate, line.LineSubtotal, line.LineTax, line.LineTotal
                }),
                invoice.Subtotal,
                invoice.TaxTotal,
                invoice.Total,
                message = "پیش‌نویس فاکتور ساخته شد؛ ثبت نهایی و سند حسابداری ایجاد نشده است."
            });
        }).RequireAuthorization("TenantWrite");
    }
}

public sealed record AssistantInvoiceDraftRequest(
    string? Kind,
    string? PartyName,
    DateTime? InvoiceDate,
    string? Number,
    string? Notes,
    List<AssistantInvoiceDraftLineRequest>? Lines);

public sealed record AssistantInvoiceDraftLineRequest(
    string? Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal TaxRate);
