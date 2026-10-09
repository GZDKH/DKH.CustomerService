using System.Text.Json.Serialization;
using DKH.CustomerService.Domain.Entities.ProductCollection;

namespace DKH.CustomerService.Domain.ValueObjects;

/// <summary>A copied option identity, label and exact canonical typed value.</summary>
public sealed record ExperienceFrozenOption
{
    [JsonConstructor]
    public ExperienceFrozenOption(Guid id, string label, ProductExperienceObservationValueType valueType,
        string? textValue, decimal? decimalValue, long? integerValue, bool? booleanValue, int displayOrder = 0)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(label) || textValue is { Length: > 2000 })
        {
            throw new ArgumentException("A frozen option requires an identity, label and bounded value.");
        }

        ProductExperienceObservationRules.ValidateTypedValue(valueType,
            textValue is not null, decimalValue.HasValue, integerValue.HasValue, booleanValue.HasValue);
        if (decimalValue.HasValue)
        {
            ExperienceObservationValue.ValidateDecimal(decimalValue.Value);
        }

        Id = id;
        Label = label;
        ValueType = valueType;
        TextValue = textValue;
        DecimalValue = decimalValue;
        IntegerValue = integerValue;
        BooleanValue = booleanValue;
        DisplayOrder = displayOrder;
    }

    public Guid Id { get; }

    public string Label { get; }

    public ProductExperienceObservationValueType ValueType { get; }

    public string? TextValue { get; }

    public decimal? DecimalValue { get; }

    public long? IntegerValue { get; }

    public bool? BooleanValue { get; }

    public int DisplayOrder { get; }

    public bool Matches(ExperienceObservationValue value, decimal? canonicalNumber)
        => ValueType == value.ValueType && TextValue == value.TextValue && BooleanValue == value.BooleanValue
            && (ValueType == ProductExperienceObservationValueType.Decimal ? DecimalValue == canonicalNumber : DecimalValue is null)
            && (ValueType == ProductExperienceObservationValueType.Integer ? IntegerValue == canonicalNumber : IntegerValue is null);
}
