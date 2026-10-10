using DKH.CustomerService.Domain.Entities.ProductCollection;

namespace DKH.CustomerService.Domain.ValueObjects;

public sealed record ExperienceObservationValue
{
    public const decimal MaximumDecimal = 999999999999.999999m;

    private ExperienceObservationValue(
        Guid definitionId,
        ProductExperienceObservationRole role,
        ProductExperienceObservationValueType valueType,
        string? textValue,
        decimal? decimalValue,
        long? integerValue,
        bool? booleanValue,
        string? unitCode,
        Guid? rowId,
        int? rowOrdinal,
        Guid? optionId)
    {
        DefinitionId = definitionId;
        Role = role;
        ValueType = valueType;
        TextValue = textValue;
        DecimalValue = decimalValue;
        IntegerValue = integerValue;
        BooleanValue = booleanValue;
        UnitCode = unitCode;
        RowId = rowId;
        RowOrdinal = rowOrdinal;
        OptionId = optionId;
    }

    public Guid DefinitionId { get; }

    public ProductExperienceObservationRole Role { get; }

    public ProductExperienceObservationValueType ValueType { get; }

    public string? TextValue { get; }

    public decimal? DecimalValue { get; }

    public long? IntegerValue { get; }

    public bool? BooleanValue { get; }

    public string? UnitCode { get; }

    public Guid? RowId { get; }

    public int? RowOrdinal { get; }

    public Guid? OptionId { get; }

    public static ExperienceObservationValue Create(
        Guid definitionId,
        ProductExperienceObservationRole role,
        ProductExperienceObservationValueType valueType,
        string? textValue = null,
        decimal? decimalValue = null,
        long? integerValue = null,
        bool? booleanValue = null,
        string? unitCode = null,
        Guid? rowId = null,
        int? rowOrdinal = null,
        Guid? optionId = null)
    {
        ProductExperienceObservationRules.ValidateDefinitionAndLimits(definitionId, role, valueType, textValue, unitCode);
        ProductExperienceObservationRules.ValidateTypedValue(
            valueType, textValue is not null, decimalValue.HasValue, integerValue.HasValue, booleanValue.HasValue);
        if (decimalValue.HasValue)
        {
            ValidateDecimal(decimalValue.Value);
        }

        if (rowId.HasValue != rowOrdinal.HasValue || rowId == Guid.Empty || rowOrdinal is < 1 or > 12)
        {
            throw new ArgumentException("Repeated observation row requires a nonempty id and ordinal from 1 through 12.");
        }

        if (optionId == Guid.Empty)
        {
            throw new ArgumentException("An option identifier must be nonempty when supplied.", nameof(optionId));
        }

        return new ExperienceObservationValue(
            definitionId, role, valueType, textValue, decimalValue, integerValue, booleanValue,
            string.IsNullOrWhiteSpace(unitCode) ? null : unitCode.Trim(), rowId, rowOrdinal, optionId);
    }

    public static void ValidateDecimal(decimal value)
    {
        if (value < -MaximumDecimal || value > MaximumDecimal || decimal.Round(value, 6) != value)
        {
            throw new ArgumentException("Numeric observation must be exactly representable as decimal (18,6).", nameof(value));
        }
    }
}
