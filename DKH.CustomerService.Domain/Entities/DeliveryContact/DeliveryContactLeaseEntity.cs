using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.DeliveryContact;

/// <summary>A bounded access receipt; it never contains address, name, email or phone content.</summary>
public sealed class DeliveryContactLeaseEntity : FullAuditedEntityWithKey<Guid>
{
    private DeliveryContactLeaseEntity()
    {
        ContextDigest = string.Empty;
        RequestDigest = string.Empty;
    }

    public Guid ActorAccountId { get; private set; }
    public Guid StorefrontId { get; private set; }
    public Guid? WorkspaceId { get; private set; }
    public Guid ContactRevisionId { get; private set; }
    public Guid? ConsentId { get; private set; }
    public long? ConsentRevision { get; private set; }
    public DeliveryContactPurpose Purpose { get; private set; }
    public string ContextDigest { get; private set; }
    public string RequestDigest { get; private set; }
    public Guid OperationId { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static DeliveryContactLeaseEntity Issue(
        Guid actorAccountId,
        DeliveryContactRevisionEntity contact,
        DeliveryContactPurpose purpose,
        string contextDigest,
        string requestDigest,
        Guid operationId,
        DateTimeOffset now,
        DateTimeOffset authorityExpiresAt,
        DeliveryContactConsentEntity? consent = null,
        Guid? workspaceId = null)
    {
        if (actorAccountId == Guid.Empty || operationId == Guid.Empty || contact.IsDeleted ||
            now.Offset != TimeSpan.Zero || authorityExpiresAt.Offset != TimeSpan.Zero || authorityExpiresAt <= now ||
            !IsDigest(contextDigest) || !IsDigest(requestDigest) ||
            purpose is not (DeliveryContactPurpose.OwnLiabilityCoordination or DeliveryContactPurpose.LocalShipment) ||
            purpose == DeliveryContactPurpose.OwnLiabilityCoordination &&
            (actorAccountId != contact.OwnerAccountId || consent is not null || workspaceId is not null) ||
            purpose == DeliveryContactPurpose.LocalShipment &&
            (consent is null || workspaceId is null || workspaceId == Guid.Empty))
        {
            throw new ArgumentException("A current purpose-bound contact authority is required.");
        }

        var expires = now.AddMinutes(5);
        if (authorityExpiresAt < expires)
        {
            expires = authorityExpiresAt;
        }

        if (consent is not null)
        {
            if (consent.IsDeleted || consent.RevokedAtUtc is not null || consent.ValidUntilUtc <= now ||
                consent.OwnerAccountId != contact.OwnerAccountId || consent.StorefrontId != contact.StorefrontId ||
                consent.ContactRevisionId != contact.Id || consent.ContactRevision != contact.Revision ||
                consent.ContactDigest != contact.Digest || consent.ScopeDigest != contextDigest)
            {
                throw new InvalidOperationException("The contact consent is unavailable.");
            }

            if (consent.ValidUntilUtc < expires)
            {
                expires = consent.ValidUntilUtc;
            }
        }

        return new DeliveryContactLeaseEntity
        {
            Id = Guid.NewGuid(),
            ActorAccountId = actorAccountId,
            StorefrontId = contact.StorefrontId,
            WorkspaceId = workspaceId,
            ContactRevisionId = contact.Id,
            ConsentId = consent?.Id,
            ConsentRevision = consent?.Revision,
            Purpose = purpose,
            ContextDigest = contextDigest,
            RequestDigest = requestDigest,
            OperationId = operationId,
            IssuedAtUtc = now,
            ExpiresAtUtc = expires,
        };
    }

    public bool MatchesAccess(Guid actorAccountId, Guid storefrontId, DeliveryContactPurpose purpose,
        string contextDigest, DateTimeOffset now, Guid? workspaceId = null)
        => !IsDeleted && ActorAccountId == actorAccountId && StorefrontId == storefrontId &&
           WorkspaceId == workspaceId && Purpose == purpose && ContextDigest == contextDigest && now >= IssuedAtUtc && now < ExpiresAtUtc;

    private static bool IsDigest(string value)
        => value is not null && value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
