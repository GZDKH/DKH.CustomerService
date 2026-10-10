using System.Numerics;
using System.Text.Json.Serialization;

namespace DKH.CustomerService.Domain.ValueObjects;

/// <summary>Resolved, frozen conversion into a definition's canonical numeric unit.</summary>
public sealed record ExperienceFrozenUnit
{
    [JsonConstructor]
    public ExperienceFrozenUnit(string code, string label, decimal multiplier, decimal offset)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 32 || code != code.Trim()
            || string.IsNullOrWhiteSpace(label) || multiplier <= 0)
        {
            throw new ArgumentException("A frozen unit requires a bounded code, display label and positive conversion multiplier.");
        }

        Code = code;
        Label = label;
        Multiplier = multiplier;
        Offset = offset;
    }

    public string Code { get; }

    public string Label { get; }

    public decimal Multiplier { get; }

    public decimal Offset { get; }

    public decimal ToCanonical(decimal value)
    {
        try
        {
            var (valueCoefficient, valueScale) = Parts(value);
            var (multiplierCoefficient, multiplierScale) = Parts(Multiplier);
            var product = checked(value * Multiplier);
            RequireExact(product, valueCoefficient * multiplierCoefficient, valueScale + multiplierScale);

            var (productCoefficient, productScale) = Parts(product);
            var (offsetCoefficient, offsetScale) = Parts(Offset);
            var sumScale = Math.Max(productScale, offsetScale);
            var sumCoefficient = productCoefficient * BigInteger.Pow(10, sumScale - productScale)
                + offsetCoefficient * BigInteger.Pow(10, sumScale - offsetScale);
            var result = checked(product + Offset);
            RequireExact(result, sumCoefficient, sumScale);
            return result;
        }
        catch (OverflowException)
        {
            throw new ArgumentException("Observation conversion exceeds supported numeric precision.", nameof(value));
        }
    }

    // Decimal arithmetic can round without overflow. Compare each result with
    // its exact integer coefficient/scale before bounds or options can see it.
    private static void RequireExact(decimal value, BigInteger coefficient, int scale)
    {
        var (resultCoefficient, resultScale) = Parts(value);
        if (resultCoefficient * BigInteger.Pow(10, scale) != coefficient * BigInteger.Pow(10, resultScale))
        {
            throw new ArgumentException("Observation conversion loses numeric precision.", nameof(value));
        }
    }

    private static (BigInteger Coefficient, int Scale) Parts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
        return ((bits[3] & int.MinValue) == 0 ? coefficient : -coefficient, (bits[3] >> 16) & 0xff);
    }
}
