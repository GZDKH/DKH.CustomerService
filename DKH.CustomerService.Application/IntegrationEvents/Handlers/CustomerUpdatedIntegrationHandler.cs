using DKH.CustomerService.Application.IntegrationEvents.Payloads;
using DKH.CustomerService.Domain.Events;
using DKH.Platform.Outbox;

namespace DKH.CustomerService.Application.IntegrationEvents.Handlers;

public sealed class CustomerUpdatedIntegrationHandler(IPlatformEventPublisher publisher)
    : INotificationHandler<CustomerUpdatedDomainEvent>
{
    public Task Handle(CustomerUpdatedDomainEvent notification, CancellationToken cancellationToken)
        => publisher.PublishAsync(
            CustomerEventTypes.CustomerUpdated,
            new CustomerUpdatedPayload(
                notification.CustomerId,
                notification.StorefrontId,
                notification.UserId),
            entityId: notification.CustomerId.ToString(),
            cancellationToken: cancellationToken);
}
