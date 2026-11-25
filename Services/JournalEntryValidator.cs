namespace GhoghnoosAcc.Services;

public sealed class JournalEntryValidator
{
    public JournalEntryValidationResult Validate(JournalEntryValidationRequest? request)
    {
        if (request is null)
            return JournalEntryValidationResult.Invalid("Ø¯Ø±Ø®ÙˆØ§Ø³Øª Ø«Ø¨Øª Ø³Ù†Ø¯ Ø®Ø§Ù„ÛŒ Ø§Ø³Øª.");

        if (request.EntryDate == default)
            return JournalEntryValidationResult.Invalid("ØªØ§Ø±ÛŒØ® Ø³Ù†Ø¯ Ù…Ø¹ØªØ¨Ø± Ù†ÛŒØ³Øª.");

        if (request.Lines is null || request.Lines.Count < 2)
            return JournalEntryValidationResult.Invalid("Ø³Ù†Ø¯ Ø¨Ø§ÛŒØ¯ Ø­Ø¯Ø§Ù‚Ù„ Ø¯Ùˆ Ø±Ø¯ÛŒÙ Ø¯Ø§Ø´ØªÙ‡ Ø¨Ø§Ø´Ø¯.");

        decimal debitTotal = 0m;
        decimal creditTotal = 0m;

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            if (line is null)
                return JournalEntryValidationResult.Invalid($"Ø±Ø¯ÛŒÙ {i + 1} Ø®Ø§Ù„ÛŒ Ø§Ø³Øª.");

            if (string.IsNullOrWhiteSpace(line.AccountName))
                return JournalEntryValidationResult.Invalid($"Ù†Ø§Ù… Ø­Ø³Ø§Ø¨ Ø¯Ø± Ø±Ø¯ÛŒÙ {i + 1} Ø§Ù„Ø²Ø§Ù…ÛŒ Ø§Ø³Øª.");

            if (line.Debit < 0m || line.Credit < 0m)
                return JournalEntryValidationResult.Invalid($"Ù…Ø¨Ù„Øº Ø±Ø¯ÛŒÙ {i + 1} Ù†Ù…ÛŒâ€ŒØªÙˆØ§Ù†Ø¯ Ù…Ù†ÙÛŒ Ø¨Ø§Ø´Ø¯.");

            if (line.Debit > 0m && line.Credit > 0m)
                return JournalEntryValidationResult.Invalid($"Ø±Ø¯ÛŒÙ {i + 1} Ù†Ù…ÛŒâ€ŒØªÙˆØ§Ù†Ø¯ Ù‡Ù…â€ŒØ²Ù…Ø§Ù† Ø¨Ø¯Ù‡Ú©Ø§Ø± Ùˆ Ø¨Ø³ØªØ§Ù†Ú©Ø§Ø± Ø¨Ø§Ø´Ø¯.");

            if (line.Debit == 0m && line.Credit == 0m)
                return JournalEntryValidationResult.Invalid($"Ø¨Ø±Ø§ÛŒ Ø±Ø¯ÛŒÙ {i + 1} Ù…Ø¨Ù„Øº Ø¨Ø¯Ù‡Ú©Ø§Ø± ÛŒØ§ Ø¨Ø³ØªØ§Ù†Ú©Ø§Ø± ÙˆØ§Ø±Ø¯ Ú©Ù†ÛŒØ¯.");

            if (decimal.Round(line.Debit, 2) != line.Debit ||
                decimal.Round(line.Credit, 2) != line.Credit)
                return JournalEntryValidationResult.Invalid($"Ù…Ø¨Ù„Øº Ø±Ø¯ÛŒÙ {i + 1} Ø­Ø¯Ø§Ú©Ø«Ø± Ø¨Ø§ÛŒØ¯ Ø¯Ùˆ Ø±Ù‚Ù… Ø§Ø¹Ø´Ø§Ø± Ø¯Ø§Ø´ØªÙ‡ Ø¨Ø§Ø´Ø¯.");

            debitTotal += line.Debit;
            creditTotal += line.Credit;
        }

        var balanced = debitTotal == creditTotal;
        return new JournalEntryValidationResult(
            IsValid: balanced,
            IsBalanced: balanced,
            DebitTotal: debitTotal,
            CreditTotal: creditTotal,
            Difference: debitTotal - creditTotal,
            Message: balanced
                ? "Ø³Ù†Ø¯ Ø§Ø² Ù†Ø¸Ø± ØªØ±Ø§Ø² Ø¨Ø¯Ù‡Ú©Ø§Ø± Ùˆ Ø¨Ø³ØªØ§Ù†Ú©Ø§Ø± Ù…Ø¹ØªØ¨Ø± Ø§Ø³Øª. Ø§ÛŒÙ† Ø¨Ø±Ø±Ø³ÛŒ Ù‡Ù†ÙˆØ² Ø³Ù†Ø¯ Ø±Ø§ Ø¯Ø± Ù¾Ø§ÛŒÚ¯Ø§Ù‡ Ø¯Ø§Ø¯Ù‡ Ø°Ø®ÛŒØ±Ù‡ Ù†Ù…ÛŒâ€ŒÚ©Ù†Ø¯."
                : "Ø³Ù†Ø¯ ØªØ±Ø§Ø² Ù†ÛŒØ³ØªØ› Ø¬Ù…Ø¹ Ø¨Ø¯Ù‡Ú©Ø§Ø± Ùˆ Ø¨Ø³ØªØ§Ù†Ú©Ø§Ø± Ø¨Ø§ÛŒØ¯ Ø¨Ø±Ø§Ø¨Ø± Ø¨Ø§Ø´Ø¯.");
    }
}

public sealed record JournalEntryValidationRequest(
    DateTime EntryDate,
    string? Description,
    List<JournalEntryValidationLine>? Lines);

public sealed record JournalEntryValidationLine(
    string? AccountName,
    decimal Debit,
    decimal Credit,
    string? PartyName = null);

public sealed record JournalEntryValidationResult(
    bool IsValid,
    bool IsBalanced,
    decimal DebitTotal,
    decimal CreditTotal,
    decimal Difference,
    string Message)
{
    public static JournalEntryValidationResult Invalid(string message) =>
        new(false, false, 0m, 0m, 0m, message);
}
