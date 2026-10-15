using DKH.CustomerService.Domain.Entities.DeliveryContact;

namespace DKH.CustomerService.Application.DeliveryContacts;

public interface IDeliveryContactStore
{
    Task<DeliveryContactView> SealAsync(DeliveryContactActor actor, Guid addressId, Guid operationId, string requestDigest, CancellationToken cancellationToken);
    Task<DeliveryContactAccessView> ResolveLiabilityAsync(DeliveryContactActor actor, DeliveryContactAuthority contact,
        Guid intendedPlanRevisionId, Guid legId, Guid operationId, Guid? accessId, string requestDigest, CancellationToken cancellationToken);
    Task<DeliveryContactConsentView> ConsentAsync(DeliveryContactActor actor, DeliveryContactAuthority contact,
        DeliveryContactScope scope, DateTimeOffset validUntilUtc, Guid operationId, string requestDigest, CancellationToken cancellationToken);
    Task<DeliveryContactConsentView> RevokeAsync(DeliveryContactActor actor, Guid consentId, long expectedRevision,
        Guid operationId, string requestDigest, CancellationToken cancellationToken);
    Task<DeliveryRecipientContactView> ResolveRecipientAsync(DeliveryContactActor actor, DeliveryContactAuthority contact,
        Guid consentId, DeliveryContactScope scope, Guid operationId, Guid? accessId, string requestDigest, CancellationToken cancellationToken);
}
