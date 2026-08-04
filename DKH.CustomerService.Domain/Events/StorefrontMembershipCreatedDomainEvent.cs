using DKH.Platform.Domain.Events;

namespace DKH.CustomerService.Domain.Events;

public sealed record StorefrontMembershipCreatedDomainEvent(
    Guid StorefrontMembershipId,
    Guid CustomerAccountId,
    Guid StorefrontId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
