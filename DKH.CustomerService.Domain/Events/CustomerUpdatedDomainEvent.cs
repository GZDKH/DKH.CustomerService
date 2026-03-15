using DKH.Platform.Domain.Events;

namespace DKH.CustomerService.Domain.Events;

public sealed record CustomerUpdatedDomainEvent(
    Guid CustomerId,
    Guid StorefrontId,
    string UserId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
