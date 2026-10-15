using DKH.CustomerService.Domain.Entities.DeliveryContact;

namespace DKH.CustomerService.Application.DeliveryContacts;

// ProfileId means CustomerProfile.Id; it is not a global account, linked identity,
// membership ID, JWT subject or an inferred Delivery participant identity.
public sealed record DeliveryContactView(DeliveryContactAuthority Authority, Guid OwnerProfileId);
