using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.DeliveryContact;

/// <summary>
/// Recipient-issued permission for a particular intended scope. Current Delivery
/// authorization remains independently necessary before another actor receives contact data.
/// </summary>
public sealed class DeliveryContactConsentEntity : FullAuditedEntityWithKey<Guid>
{
    private DeliveryContactConsentEntity()
    {
        ScopeJson = string.Empty;
        ScopeDigest = string.Empty;
        ContactDigest = string.Empty;
    }

    public Guid OwnerAccountId { get; private set; }
    public Guid StorefrontId { get; private set; }
    public Guid ContactRevisionId { get; private set; }
    public long ContactRevision { get; private set; }
    public string ContactDigest { get; private set; }
    public string ScopeJson { get; private set; }
    public string ScopeDigest { get; private set; }
    public long Revision { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestDigest { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ValidUntilUtc { get; private set; }
    public string? RevokeRequestDigest { get; private set; }
    public Guid? RevokeOperationId { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static DeliveryContactConsentEntity Record(
        DeliveryContactRevisionEntity contact,
        DeliveryContactScope scope,
        Guid operationId,
        DateTimeOffset now,
        DateTimeOffset validUntil, string requestDigest)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        if (requestDigest is null || requestDigest.Length != 64 || requestDigest.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A canonical request digest is required.");
        }

        if (contact.IsDeleted || operationId == Guid.Empty || now.Offset != TimeSpan.Zero ||
            validUntil.Offset != TimeSpan.Zero || validUntil <= now || validUntil > now.AddMinutes(5))
        {
            throw new ArgumentException("An owned current contact and future consent interval are required.");
        }

        return new DeliveryContactConsentEntity
        {
            Id = Guid.NewGuid(),
            OwnerAccountId = contact.OwnerAccountId,
            StorefrontId = contact.StorefrontId,
            ContactRevisionId = contact.Id,
            ContactRevision = contact.Revision,
            ContactDigest = contact.Digest,
            ScopeJson = System.Text.Json.JsonSerializer.Serialize(scope),
            ScopeDigest = scope.Digest(),
            Revision = 1,
            OperationId = operationId,
            RequestDigest = requestDigest,
            CreatedAtUtc = now,
            ValidUntilUtc = validUntil,
        };
    }

    public bool Permits(DeliveryContactRevisionEntity contact, DeliveryContactScope scope, DateTimeOffset now)
        => !IsDeleted && !contact.IsDeleted && RevokedAtUtc is null && now >= CreatedAtUtc && now < ValidUntilUtc &&
           OwnerAccountId == contact.OwnerAccountId && StorefrontId == contact.StorefrontId &&
           ContactRevisionId == contact.Id && ContactRevision == contact.Revision &&
           ContactDigest == contact.Digest && ScopeDigest == scope.Digest();

    public bool MatchesRevocationReplay(Guid targetConsentId, Guid operationId, long expectedRevision, string requestDigest)
        => !IsDeleted && Id == targetConsentId && RevokedAtUtc is not null &&
           RevokeOperationId == operationId && expectedRevision == Revision - 1 && RevokeRequestDigest == requestDigest;

    public void Revoke(Guid operationId, long expectedRevision, DateTimeOffset now, string requestDigest)
    {
        if (operationId == Guid.Empty || now.Offset != TimeSpan.Zero || now < CreatedAtUtc || string.IsNullOrEmpty(requestDigest) || requestDigest.Length != 64 ||
            requestDigest.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A revocation operation is required.");
        }

        if (RevokedAtUtc is not null && RevokeOperationId == operationId && expectedRevision == Revision - 1 && RevokeRequestDigest == requestDigest)
        {
            return;
        }

        if (Revision != expectedRevision || RevokedAtUtc is not null)
        {
            throw new InvalidOperationException("The contact consent revision changed.");
        }

        RevokeOperationId = operationId;
        RevokeRequestDigest = requestDigest;
        RevokedAtUtc = now;
        Revision = checked(Revision + 1);
    }
}
