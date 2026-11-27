namespace GhoghnoosAcc.Services;

/// <summary>
/// Validates decimal scale before values are persisted to fixed-precision accounting columns.
/// </summary>
public static class InvoiceDecimalPrecision
{
    public static bool HasScaleAbove(decimal value, int maxScale)
    {
        if (maxScale < 0 || maxScale > 28)
            throw new ArgumentOutOfRangeException(nameof(maxScale));

        var bits = decimal.GetBits(value);
        var scale = (bits[3] >> 16) & 0x7F;
        return scale > maxScale;
    }
}
