using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ProductCollection;

public sealed class ProductExperienceTagEntity : FullAuditedEntityWithKey<Guid>
{
    private ProductExperienceTagEntity()
    {
        Id = Guid.Empty;
        ExperienceId = Guid.Empty;
        Value = string.Empty;
    }

    private ProductExperienceTagEntity(Guid experienceId, string value)
        : base(Guid.NewGuid())
    {
        ExperienceId = experienceId;
        Value = value;
    }

    public Guid ExperienceId { get; private set; }

    public string Value { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static ProductExperienceTagEntity Create(Guid experienceId, string value)
    {
        if (experienceId == Guid.Empty)
        {
            throw new ArgumentException("Experience id is required.", nameof(experienceId));
        }

        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 64)
        {
            throw new ArgumentException("Tag must contain 1 to 64 non-whitespace characters.", nameof(value));
        }

        return new ProductExperienceTagEntity(experienceId, normalized);
    }
}
