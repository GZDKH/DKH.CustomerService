using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Domain.Events;
using DKH.Platform.Domain.Entities.Auditing;
using DKH.Platform.Domain.Events;
using DKH.Platform.MultiTenancy;

namespace DKH.CustomerService.Domain.Entities.StorefrontMembership;

public sealed class StorefrontMembershipEntity : FullAuditedEntityWithKey<Guid>,
    IAggregateRoot,
    IPlatformStorefrontScoped
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private StorefrontMembershipEntity()
    {
        Id = Guid.Empty;
        CustomerAccountId = Guid.Empty;
        StorefrontId = Guid.Empty;
        Status = StorefrontMembershipStatusType.Active;
    }

    private StorefrontMembershipEntity(
        Guid customerAccountId,
        Guid storefrontId,
        Guid? legacyCustomerProfileId,
        DateTime authenticatedAt)
        : base(Guid.NewGuid())
    {
        CustomerAccountId = RequireNonEmpty(customerAccountId, nameof(customerAccountId));
        StorefrontId = RequireNonEmpty(storefrontId, nameof(storefrontId));
        LegacyCustomerProfileId = legacyCustomerProfileId;
        FirstAuthenticatedAt = EnsureUtc(authenticatedAt);
        LastAuthenticatedAt = FirstAuthenticatedAt;
        LastActivityAt = FirstAuthenticatedAt;
        Status = StorefrontMembershipStatusType.Active;
    }

    public Guid CustomerAccountId { get; private set; }

    public Guid StorefrontId { get; private set; }

    public Guid? LegacyCustomerProfileId { get; private set; }

    public DateTime FirstAuthenticatedAt { get; private set; }

    public DateTime LastAuthenticatedAt { get; private set; }

    public DateTime LastActivityAt { get; private set; }

    public StorefrontMembershipStatusType Status { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public override object?[] GetKeys() => [Id];

    public static StorefrontMembershipEntity Create(
        Guid customerAccountId,
        Guid storefrontId,
        DateTime authenticatedAt,
        Guid? legacyCustomerProfileId = null)
    {
        var membership = new StorefrontMembershipEntity(
            customerAccountId,
            storefrontId,
            legacyCustomerProfileId,
            authenticatedAt);

        membership._domainEvents.Add(new StorefrontMembershipCreatedDomainEvent(
            membership.Id,
            membership.CustomerAccountId,
            membership.StorefrontId));

        return membership;
    }

    public void RegisterAuthenticatedTouch(DateTime authenticatedAt)
    {
        var timestamp = EnsureUtc(authenticatedAt);
        if (timestamp > LastAuthenticatedAt)
        {
            LastAuthenticatedAt = timestamp;
        }

        RegisterActivity(timestamp);
    }

    public void RegisterActivity(DateTime occurredAt)
    {
        var timestamp = EnsureUtc(occurredAt);
        if (timestamp > LastActivityAt)
        {
            LastActivityAt = timestamp;
        }
    }

    public void Block() => Status = StorefrontMembershipStatusType.Blocked;

    public void Activate()
    {
        if (Status == StorefrontMembershipStatusType.Revoked)
        {
            throw new InvalidOperationException("A revoked storefront membership cannot be reactivated.");
        }

        Status = StorefrontMembershipStatusType.Active;
    }

    public void Revoke() => Status = StorefrontMembershipStatusType.Revoked;

    public void ClearDomainEvents() => _domainEvents.Clear();

    private static Guid RequireNonEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{parameterName} must not be empty.", parameterName);
        }

        return value;
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
