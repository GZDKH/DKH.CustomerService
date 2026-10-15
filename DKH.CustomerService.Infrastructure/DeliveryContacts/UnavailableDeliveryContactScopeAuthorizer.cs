using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Domain.Entities.DeliveryContact;

namespace DKH.CustomerService.Infrastructure.DeliveryContacts;

/// <summary>Honest production default while Delivery has no authenticated owner resolver.
/// Self-owned contact sealing/consent is available; this never grants recipient access.</summary>
public sealed class UnavailableDeliveryContactScopeAuthorizer : IDeliveryContactScopeAuthorizer
{
    public Task<DeliveryContactScopeDecision> RevalidateAsync(Guid actorAccountId, Guid storefrontId,
        DeliveryContactScope scope, CancellationToken cancellationToken)
        => throw new DeliveryContactFailureException("AUTHORITY_UNAVAILABLE");
}
