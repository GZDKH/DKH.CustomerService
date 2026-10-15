using DKH.CustomerService.Domain.Entities.DeliveryContact;

namespace DKH.CustomerService.Application.DeliveryContacts;

public interface IDeliveryContactScopeAuthorizer
{
    Task<DeliveryContactScopeDecision> RevalidateAsync(Guid actorAccountId, Guid storefrontId,
        DeliveryContactScope scope, CancellationToken cancellationToken);
}
