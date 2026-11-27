namespace GhoghnoosAcc.Services;

/// <summary>
/// Validates decimal scale and numeric(18,2) range before accounting values are persisted.
/// </summary>
public static class InvoiceDecimalPrecision
{
    public const decimal MaxNumeric18_2 = 9_999_999_999_999_999.99m;

    public static bool HasScaleAbove(decimal value, int maxScale)
    {
        if (maxScale < 0 || maxScale > 28)
            throw new ArgumentOutOfRangeException(nameof(maxScale));

        var bits = decimal.GetBits(value);
        var scale = (bits[3] >> 16) & 0x7F;
        return scale > maxScale;
    }

    public static bool FitsNumeric18_2(decimal value) =>
        value >= -MaxNumeric18_2 && value <= MaxNumeric18_2;
}
