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
            return checked((value * Multiplier) + Offset);
        }
        catch (OverflowException)
        {
            throw new ArgumentException("Observation conversion exceeds supported numeric precision.", nameof(value));
        }
    }
}
