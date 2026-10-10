namespace DKH.CustomerService.Domain.Entities.ProductCollection;

/// <summary>Typed observation shape shared by legacy summaries and dated revisions.</summary>
public static class ProductExperienceObservationRules
{
    public static void ValidateDefinitionAndLimits(
        Guid definitionId,
        ProductExperienceObservationRole role,
        ProductExperienceObservationValueType valueType,
        string? textValue,
        string? unitCode)
    {
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
    }

    public static void ValidateTypedValue(
        ProductExperienceObservationValueType valueType,
        bool hasText,
        bool hasDecimal,
        bool hasInteger,
        bool hasBoolean)
    {
        var supplied = (hasText ? 1 : 0) + (hasDecimal ? 1 : 0)
            + (hasInteger ? 1 : 0) + (hasBoolean ? 1 : 0);
        if (supplied != 1)
        {
            throw new ArgumentException("Exactly one typed value is required.");
        }

        var validType = valueType switch
        {
            ProductExperienceObservationValueType.Text => hasText,
            ProductExperienceObservationValueType.Decimal => hasDecimal,
            ProductExperienceObservationValueType.Integer => hasInteger,
            ProductExperienceObservationValueType.Boolean => hasBoolean,
            _ => false,
        };
        if (!validType)
        {
            throw new ArgumentException("Typed value does not match value type.");
        }
    }
}
