using DKH.CustomerService.Application.IntegrationEvents.Payloads;
using DKH.CustomerService.Domain.Events;
using DKH.Platform.Outbox;

namespace DKH.CustomerService.Application.IntegrationEvents.Handlers;

public sealed class CustomerCreatedIntegrationHandler(IPlatformEventPublisher publisher)
    : INotificationHandler<CustomerCreatedDomainEvent>
{
    public Task Handle(CustomerCreatedDomainEvent notification, CancellationToken cancellationToken)
        => publisher.PublishAsync(
            CustomerEventTypes.CustomerCreated,
            new CustomerCreatedPayload(
                notification.CustomerId,
                notification.StorefrontId,
                notification.UserId,
                notification.FirstName,
                null,
                null,
                null,
                "en"),
            entityId: notification.CustomerId.ToString(),
            cancellationToken: cancellationToken);
}
