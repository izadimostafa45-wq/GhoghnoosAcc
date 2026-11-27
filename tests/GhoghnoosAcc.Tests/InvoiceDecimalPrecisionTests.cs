using GhoghnoosAcc.Services;

namespace GhoghnoosAcc.Tests;

public sealed class InvoiceDecimalPrecisionTests
{
    [Theory]
    [InlineData("12.123")]
    [InlineData("0.001")]
    public void Quantity_AllowsAtMostThreeDecimalPlaces(string rawValue)
    {
        var value = decimal.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(InvoiceDecimalPrecision.HasScaleAbove(value, 3));
    }

    [Fact]
    public void Quantity_RejectsMoreThanThreeDecimalPlaces()
    {
        Assert.True(InvoiceDecimalPrecision.HasScaleAbove(12.1234m, 3));
    }

    [Theory]
    [InlineData("12.12")]
    [InlineData("0.01")]
    [InlineData("0")]
    public void MonetaryAndTaxValues_AllowAtMostTwoDecimalPlaces(string rawValue)
    {
        var value = decimal.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(InvoiceDecimalPrecision.HasScaleAbove(value, 2));
    }

    [Fact]
    public void MonetaryAndTaxValues_RejectMoreThanTwoDecimalPlaces()
    {
        Assert.True(InvoiceDecimalPrecision.HasScaleAbove(12.123m, 2));
    }

    [Fact]
    public void ScaleCheck_DoesNotRoundOrModifyValue()
    {
        const decimal value = 12.1234m;

        Assert.True(InvoiceDecimalPrecision.HasScaleAbove(value, 2));
        Assert.Equal(12.1234m, value);
    }

    [Fact]
    public void Numeric18_2_AcceptsMaximumPositiveValue()
    {
        Assert.True(InvoiceDecimalPrecision.FitsNumeric18_2(9_999_999_999_999_999.99m));
    }

    [Fact]
    public void Numeric18_2_AcceptsMaximumNegativeValue()
    {
        Assert.True(InvoiceDecimalPrecision.FitsNumeric18_2(-9_999_999_999_999_999.99m));
    }

    [Theory]
    [InlineData("10000000000000000.00")]
    [InlineData("-10000000000000000.00")]
    public void Numeric18_2_RejectsValuesOutsideDatabaseRange(string rawValue)
    {
        var value = decimal.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture);

        Assert.False(InvoiceDecimalPrecision.FitsNumeric18_2(value));
    }
}
