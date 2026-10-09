namespace DKH.CustomerService.Domain.ValueObjects;

/// <summary>Retained target identity and label; catalog visibility is verified by its owner adapter.</summary>
public sealed record ExperienceTarget
{
    private ExperienceTarget(Guid? productId, Guid? unknownReferenceId, Guid? releaseId, string retainedLabel)
    {
        ProductId = productId;
        UnknownReferenceId = unknownReferenceId;
        ReleaseId = releaseId;
        RetainedLabel = retainedLabel;
    }

    public Guid? ProductId { get; }

    public Guid? UnknownReferenceId { get; }

    public Guid? ReleaseId { get; }

    public string RetainedLabel { get; }

    public static ExperienceTarget Create(Guid? productId, Guid? unknownReferenceId, Guid? releaseId, string retainedLabel)
    {
        if (productId.HasValue == unknownReferenceId.HasValue
            || productId == Guid.Empty || unknownReferenceId == Guid.Empty || releaseId == Guid.Empty
            || (releaseId.HasValue && !productId.HasValue))
        {
            throw new ArgumentException("Select one nonempty Product or private unknown reference; a Release requires a Product.");
        }

        if (string.IsNullOrWhiteSpace(retainedLabel) || retainedLabel.Length > 256)
        {
            throw new ArgumentException("A retained target label from 1 through 256 characters is required.", nameof(retainedLabel));
        }

        return new ExperienceTarget(productId, unknownReferenceId, releaseId, retainedLabel);
    }
}
