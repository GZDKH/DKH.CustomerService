using DKH.CustomerService.Application.IntegrationEvents.Payloads;
using DKH.CustomerService.Domain.Events;
using DKH.Platform.Outbox;

namespace DKH.CustomerService.Application.IntegrationEvents.Handlers;

public sealed class CustomerSegmentChangedIntegrationHandler(IPlatformEventPublisher publisher)
    : INotificationHandler<CustomerSegmentChangedDomainEvent>
{
    public Task Handle(CustomerSegmentChangedDomainEvent notification, CancellationToken cancellationToken)
        => publisher.PublishAsync(
            CustomerEventTypes.CustomerSegmentChanged,
            new CustomerSegmentChangedPayload(
                notification.CustomerId,
                notification.StorefrontId,
                notification.OldStatus.ToString(),
                notification.NewStatus.ToString()),
            entityId: notification.CustomerId.ToString(),
            cancellationToken: cancellationToken);
}
