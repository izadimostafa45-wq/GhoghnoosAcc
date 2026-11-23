using GhoghnoosAcc.Services;

namespace GhoghnoosAcc.Tests;

public sealed class JournalEntryValidatorTests
{
    private readonly JournalEntryValidator _validator = new();

    [Fact]
    public void Validate_NullRequest_IsInvalid()
    {
        var result = _validator.Validate(null);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_DefaultDate_IsInvalid()
    {
        var result = _validator.Validate(new JournalEntryValidationRequest(
            default, null,
            [new("Cash", 100m, 0m), new("Sales", 0m, 100m)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_FewerThanTwoLines_IsInvalid()
    {
        var result = _validator.Validate(new JournalEntryValidationRequest(
            new DateTime(2026, 1, 1), null, [new("Cash", 100m, 0m)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_BalancedEntry_IsValidAndReturnsTotals()
    {
        var result = _validator.Validate(new JournalEntryValidationRequest(
            new DateTime(2026, 1, 1), "Opening balance",
            [new("Cash", 1250.50m, 0m), new("Capital", 0m, 1250.50m)]));

        Assert.True(result.IsValid);
        Assert.True(result.IsBalanced);
        Assert.Equal(1250.50m, result.DebitTotal);
        Assert.Equal(1250.50m, result.CreditTotal);
        Assert.Equal(0m, result.Difference);
    }

    [Fact]
    public void Validate_UnbalancedEntry_IsInvalidAndReturnsDifference()
    {
        var result = _validator.Validate(new JournalEntryValidationRequest(
            new DateTime(2026, 1, 1), null,
            [new("Cash", 100m, 0m), new("Capital", 0m, 90m)]));

        Assert.False(result.IsValid);
        Assert.False(result.IsBalanced);
        Assert.Equal(10m, result.Difference);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(10, 10)]
    [InlineData(0, 0)]
    public void Validate_InvalidLineAmounts_AreRejected(decimal debit, decimal credit)
    {
        var result = _validator.Validate(new JournalEntryValidationRequest(
            new DateTime(2026, 1, 1), null,
            [new("Cash", debit, credit), new("Capital", 0m, 100m)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_AmountsWithMoreThanTwoDecimalPlaces_AreRejected()
    {
        var result = _validator.Validate(new JournalEntryValidationRequest(
            new DateTime(2026, 1, 1), null,
            [new("Cash", 10.001m, 0m), new("Capital", 0m, 10.001m)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MissingAccountName_IsRejected()
    {
        var result = _validator.Validate(new JournalEntryValidationRequest(
            new DateTime(2026, 1, 1), null,
            [new(" ", 100m, 0m), new("Capital", 0m, 100m)]));

        Assert.False(result.IsValid);
    }
}
