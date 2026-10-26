using DKH.CustomerService.Domain.Entities.DeliveryContact;

namespace DKH.CustomerService.Application.DeliveryContacts;

// Private native accepted actor DATA, independent from the recipient profile.
// The genuine Delivery adapter excludes service and unknown actor variants.
public sealed record DeliveryContactActingActor(Guid? CustomerProfileId, DeliveryContactAuthority? CounterpartyAuthority);
