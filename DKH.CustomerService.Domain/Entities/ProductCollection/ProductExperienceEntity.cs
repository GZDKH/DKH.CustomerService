using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ProductCollection;

public sealed class ProductExperienceEntity : FullAuditedEntityWithKey<Guid>
{
    private readonly List<ProductExperienceObservationEntity> _observations = [];
    private readonly List<ProductExperienceTagEntity> _tags = [];

    private ProductExperienceEntity()
    {
        Id = Guid.Empty;
        CollectionItemId = Guid.Empty;
    }

    private ProductExperienceEntity(
        Guid collectionItemId,
        DateTimeOffset? experiencedAt,
        string? personalText,
        string? recommendation)
        : base(Guid.NewGuid())
    {
        CollectionItemId = collectionItemId;
        ExperiencedAt = experiencedAt;
        PersonalText = personalText;
        Recommendation = recommendation;
    }

    public Guid CollectionItemId { get; private set; }

    public ProductCollectionItemEntity? CollectionItem { get; private set; }

    public DateTimeOffset? ExperiencedAt { get; private set; }

    public string? PersonalText { get; private set; }

    public string? Recommendation { get; private set; }

    public IReadOnlyCollection<ProductExperienceObservationEntity> Observations => _observations.AsReadOnly();

    public IReadOnlyCollection<ProductExperienceTagEntity> Tags => _tags.AsReadOnly();

    public override object?[] GetKeys() => [Id];

    public static ProductExperienceEntity Create(
        Guid collectionItemId,
        DateTimeOffset? experiencedAt,
        string? personalText,
        string? recommendation,
        IEnumerable<(Guid DefinitionId, ProductExperienceObservationRole Role, ProductExperienceObservationValueType ValueType, string? TextValue, double? DecimalValue, long? IntegerValue, bool? BooleanValue, string? UnitCode)> observations,
        IEnumerable<string> tags)
    {
        if (collectionItemId == Guid.Empty)
        {
            throw new ArgumentException("Collection item id is required.", nameof(collectionItemId));
        }

        if (personalText is { Length: > 7000 })
        {
            throw new ArgumentException("Personal text must not exceed 7000 characters.", nameof(personalText));
        }

        if (recommendation is { Length: > 2000 })
        {
            throw new ArgumentException("Recommendation must not exceed 2000 characters.", nameof(recommendation));
        }

        var entity = new ProductExperienceEntity(
            collectionItemId,
            experiencedAt,
            string.IsNullOrWhiteSpace(personalText) ? null : personalText,
            string.IsNullOrWhiteSpace(recommendation) ? null : recommendation);

        var seenDefinitions = new HashSet<(Guid, ProductExperienceObservationRole)>();
        foreach (var observation in observations)
        {
            if (!seenDefinitions.Add((observation.DefinitionId, observation.Role)))
            {
                throw new ArgumentException("A definition may occur only once per observation role.", nameof(observations));
            }

            entity._observations.Add(ProductExperienceObservationEntity.Create(
                entity.Id,
                observation.DefinitionId,
                observation.Role,
                observation.ValueType,
                observation.TextValue,
                observation.DecimalValue,
                observation.IntegerValue,
                observation.BooleanValue,
                observation.UnitCode));
        }

        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            var normalized = tag?.Trim();
            if (string.IsNullOrWhiteSpace(normalized) || !seenTags.Add(normalized))
            {
                throw new ArgumentException("Tags must be unique and non-empty.", nameof(tags));
            }

            entity._tags.Add(ProductExperienceTagEntity.Create(entity.Id, normalized));
        }

        return entity;
    }
}
