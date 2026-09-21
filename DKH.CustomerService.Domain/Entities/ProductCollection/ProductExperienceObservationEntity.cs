using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ProductCollection;

public sealed class ProductExperienceObservationEntity : FullAuditedEntityWithKey<Guid>
{
    private ProductExperienceObservationEntity()
    {
        Id = Guid.Empty;
        ExperienceId = Guid.Empty;
        DefinitionId = Guid.Empty;
    }

    private ProductExperienceObservationEntity(
        Guid experienceId,
        Guid definitionId,
        ProductExperienceObservationRole role,
        ProductExperienceObservationValueType valueType,
        string? textValue,
        double? decimalValue,
        long? integerValue,
        bool? booleanValue,
        string? unitCode)
        : base(Guid.NewGuid())
    {
        ExperienceId = experienceId;
        DefinitionId = definitionId;
        Role = role;
        ValueType = valueType;
        TextValue = textValue;
        DecimalValue = decimalValue;
        IntegerValue = integerValue;
        BooleanValue = booleanValue;
        UnitCode = unitCode;
    }

    public Guid ExperienceId { get; private set; }

    public Guid DefinitionId { get; private set; }

    public ProductExperienceObservationRole Role { get; private set; }

    public ProductExperienceObservationValueType ValueType { get; private set; }

    public string? TextValue { get; private set; }

    public double? DecimalValue { get; private set; }

    public long? IntegerValue { get; private set; }

    public bool? BooleanValue { get; private set; }

    public string? UnitCode { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static ProductExperienceObservationEntity Create(
        Guid experienceId,
        Guid definitionId,
        ProductExperienceObservationRole role,
        ProductExperienceObservationValueType valueType,
        string? textValue = null,
        double? decimalValue = null,
        long? integerValue = null,
        bool? booleanValue = null,
        string? unitCode = null)
    {
        if (experienceId == Guid.Empty)
        {
            throw new ArgumentException("Experience id is required.", nameof(experienceId));
        }

        if (definitionId == Guid.Empty)
        {
            throw new ArgumentException("Definition id is required.", nameof(definitionId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        if (!Enum.IsDefined(valueType))
        {
            throw new ArgumentOutOfRangeException(nameof(valueType));
        }

        if (unitCode is { Length: > 32 })
        {
            throw new ArgumentException("Unit code must not exceed 32 characters.", nameof(unitCode));
        }

        if (textValue is { Length: > 2000 })
        {
            throw new ArgumentException("Text value must not exceed 2000 characters.", nameof(textValue));
        }

        if (decimalValue is double value && (double.IsNaN(value) || double.IsInfinity(value)))
        {
            throw new ArgumentException("Decimal value must be finite.", nameof(decimalValue));
        }

        var supplied = (textValue is not null ? 1 : 0)
            + (decimalValue.HasValue ? 1 : 0)
            + (integerValue.HasValue ? 1 : 0)
            + (booleanValue.HasValue ? 1 : 0);
        if (supplied != 1)
        {
            throw new ArgumentException("Exactly one typed value is required.");
        }

        var validType = valueType switch
        {
            ProductExperienceObservationValueType.Text => textValue is not null,
            ProductExperienceObservationValueType.Decimal => decimalValue.HasValue,
            ProductExperienceObservationValueType.Integer => integerValue.HasValue,
            ProductExperienceObservationValueType.Boolean => booleanValue.HasValue,
            _ => false,
        };
        if (!validType)
        {
            throw new ArgumentException("Typed value does not match value type.");
        }

        return new ProductExperienceObservationEntity(
            experienceId,
            definitionId,
            role,
            valueType,
            textValue,
            decimalValue,
            integerValue,
            booleanValue,
            string.IsNullOrWhiteSpace(unitCode) ? null : unitCode.Trim());
    }
}
