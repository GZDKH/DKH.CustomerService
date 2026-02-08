using DKH.Platform.Domain.Events;

namespace DKH.CustomerService.Infrastructure.Persistence;

internal sealed class NullDomainEventDispatcher : IPlatformDomainEventDispatcher
{
    private readonly bool _enabled = true;

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _ = domainEvent;
        _ = cancellationToken;
        return _enabled ? Task.CompletedTask : Task.CompletedTask;
    }
}
