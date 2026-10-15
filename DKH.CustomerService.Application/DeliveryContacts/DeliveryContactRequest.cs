using DKH.CustomerService.Domain.Entities.DeliveryContact;

namespace DKH.CustomerService.Application.DeliveryContacts;

public sealed record DeliveryContactRequest(DeliveryContactActor Actor, DeliveryContactOperation Operation,
    Guid OperationId, Guid? AddressId = null, DeliveryContactAuthority? Contact = null,
    DeliveryContactScope? Scope = null, Guid? ConsentId = null, long? ExpectedRevision = null,
    DateTimeOffset? ValidUntilUtc = null, Guid? IntendedPlanRevisionId = null, Guid? LegId = null,
    Guid? AccessId = null, string Reason = "") : IRequest<object>;
