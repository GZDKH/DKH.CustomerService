using DKH.Platform.Domain.Events;

namespace DKH.CustomerService.Domain.Events;

public sealed record CustomerAccountCreatedDomainEvent(Guid CustomerAccountId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
