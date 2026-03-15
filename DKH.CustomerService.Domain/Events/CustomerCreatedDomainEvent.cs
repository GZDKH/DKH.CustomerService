using DKH.Platform.Domain.Events;

namespace DKH.CustomerService.Domain.Events;

public sealed record CustomerCreatedDomainEvent(
    Guid CustomerId,
    Guid StorefrontId,
    string UserId,
    string? FirstName) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
