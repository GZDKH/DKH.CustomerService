using System.Text.Json.Serialization;
using DKH.CustomerService.Domain.Entities.ProductCollection;

namespace DKH.CustomerService.Domain.ValueObjects;

/// <summary>Journal-local resolved semantics; no catalog lookup or write is performed here.</summary>
public sealed record ExperienceFrozenDefinition
{
    [JsonConstructor]
    public ExperienceFrozenDefinition(Guid definitionId, Guid catalogId, Guid categoryId, string label,
        ProductExperienceObservationRole role, ProductExperienceObservationValueType valueType, bool isReadOnly,
        string? canonicalUnitCode, IReadOnlyList<ExperienceFrozenUnit> units, IReadOnlyList<ExperienceFrozenOption> options,
        bool allowCustomValues = true, decimal? minimum = null, decimal? maximum = null, int displayOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(options);
        ProductExperienceObservationRules.ValidateDefinitionAndLimits(definitionId, role, valueType, null, canonicalUnitCode);
        if (catalogId == Guid.Empty || categoryId == Guid.Empty || string.IsNullOrWhiteSpace(label)
            || minimum > maximum || units.Any(unit => unit is null) || options.Any(option => option is null))
        {
            throw new ArgumentException("A frozen definition requires exact catalog/category binding and valid resolved metadata.");
        }

        var numeric = valueType is ProductExperienceObservationValueType.Decimal or ProductExperienceObservationValueType.Integer;
        if ((!numeric && (units.Count != 0 || canonicalUnitCode is not null || minimum.HasValue || maximum.HasValue))
            || (units.Count == 0 && canonicalUnitCode is not null)
            || (units.Count != 0 && (canonicalUnitCode is null || !units.Any(unit => unit.Code == canonicalUnitCode && unit.Multiplier == 1 && unit.Offset == 0)))
            || units.Select(unit => unit.Code).Distinct(StringComparer.Ordinal).Count() != units.Count
            || options.Select(option => option.Id).Distinct().Count() != options.Count
            || options.Any(option => option.ValueType != valueType))
        {
            throw new ArgumentException("Frozen units and options must match the resolved type and canonical unit.");
        }

        DefinitionId = definitionId;
        CatalogId = catalogId;
        CategoryId = categoryId;
        Label = label;
        Role = role;
        ValueType = valueType;
        IsReadOnly = isReadOnly;
        CanonicalUnitCode = canonicalUnitCode;
        Units = Array.AsReadOnly(units.OrderBy(unit => unit.Code, StringComparer.Ordinal).ToArray());
        Options = Array.AsReadOnly(options.OrderBy(option => option.DisplayOrder).ThenBy(option => option.Id).ToArray());
        AllowCustomValues = allowCustomValues;
        Minimum = minimum;
        Maximum = maximum;
        DisplayOrder = displayOrder;
    }

    public Guid DefinitionId { get; }

    public Guid CatalogId { get; }

    public Guid CategoryId { get; }

    public string Label { get; }

    public ProductExperienceObservationRole Role { get; }

    public ProductExperienceObservationValueType ValueType { get; }

    public bool IsReadOnly { get; }

    public string? CanonicalUnitCode { get; }

    public IReadOnlyList<ExperienceFrozenUnit> Units { get; }

    public IReadOnlyList<ExperienceFrozenOption> Options { get; }

    public bool AllowCustomValues { get; }

    public decimal? Minimum { get; }

    public decimal? Maximum { get; }

    public int DisplayOrder { get; }

    public void ValidateObservation(ExperienceObservationValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (IsReadOnly || value.DefinitionId != DefinitionId || value.Role != Role || value.ValueType != ValueType)
        {
            throw new ArgumentException("Observation is not a writable field in the retained profile.", nameof(value));
        }

        decimal? number = value.DecimalValue ?? value.IntegerValue;
        if (Units.Count == 0)
        {
            if (value.UnitCode is not null)
            {
                throw new ArgumentException("Observation unit is not declared by the retained profile.", nameof(value));
            }
        }
        else
        {
            var unit = Units.SingleOrDefault(unit => unit.Code == value.UnitCode)
                ?? throw new ArgumentException("Observation unit is not declared by the retained profile.", nameof(value));
            number = unit.ToCanonical(number!.Value);
            if (ValueType == ProductExperienceObservationValueType.Decimal)
            {
                ExperienceObservationValue.ValidateDecimal(number.Value);
            }
        }

        if (number < Minimum || number > Maximum)
        {
            throw new ArgumentException("Observation is outside the frozen numeric bounds.", nameof(value));
        }

        if (value.OptionId.HasValue)
        {
            var option = Options.SingleOrDefault(option => option.Id == value.OptionId)
                ?? throw new ArgumentException("Observation option is not declared by the retained profile.", nameof(value));
            if (!option.Matches(value, number))
            {
                throw new ArgumentException("Observation value does not match its frozen option.", nameof(value));
            }
        }
        else if (Options.Count != 0 && !AllowCustomValues)
        {
            throw new ArgumentException("Observation requires a retained option identifier.", nameof(value));
        }
    }
}
