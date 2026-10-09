using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ExperienceJournal;

/// <summary>Account-local label for an explicit private unknown target, without a catalog write.</summary>
public sealed class ExperienceUnknownReferenceEntity : FullAuditedEntityWithKey<Guid>
{
    private ExperienceUnknownReferenceEntity()
    {
        OwnerLabel = string.Empty;
    }

    private ExperienceUnknownReferenceEntity(Guid accountId, string ownerLabel, string? producerLabel)
        : base(Guid.NewGuid())
    {
        AccountId = accountId;
        OwnerLabel = ownerLabel;
        ProducerLabel = producerLabel;
    }

    public Guid AccountId { get; private set; }

    public string OwnerLabel { get; private set; }

    public string? ProducerLabel { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static ExperienceUnknownReferenceEntity Create(Guid accountId, string ownerLabel, string? producerLabel = null)
    {
        if (accountId == Guid.Empty || string.IsNullOrWhiteSpace(ownerLabel) || ownerLabel.Length > 256
            || producerLabel is { Length: > 256 })
        {
            throw new ArgumentException("A canonical account and a private target label from 1 through 256 characters are required.");
        }

        return new ExperienceUnknownReferenceEntity(accountId, ownerLabel, producerLabel);
    }
}
