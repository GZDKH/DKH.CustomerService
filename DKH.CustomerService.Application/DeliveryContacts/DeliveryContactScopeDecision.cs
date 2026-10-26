namespace DKH.CustomerService.Application.DeliveryContacts;

// Only the genuine Delivery owner adapter may return this decision. There is no
// positive decision until its authenticated owner boundary and actual
// participant-to-Customer binding exist. Request selectors are not this proof.
public sealed record DeliveryContactScopeDecision(
    Domain.Entities.DeliveryContact.DeliveryContactScope Scope, Guid RecipientProfileId,
    Guid StorefrontId, Guid WorkspaceId, bool CarrierRequiresPhone, DateTimeOffset ExpiresAtUtc,
    Guid RecipientConsentId, Guid PrivateContactId, DeliveryContactActingActor? ActingActor = null);
