using DKH.CustomerService.Domain.Enums;
using DKH.Platform.Domain.Events;

namespace DKH.CustomerService.Domain.Events;

public sealed record CustomerSegmentChangedDomainEvent(
    Guid CustomerId,
    Guid StorefrontId,
    AccountStatusType OldStatus,
    AccountStatusType NewStatus) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
