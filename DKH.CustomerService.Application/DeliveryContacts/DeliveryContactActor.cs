using DKH.CustomerService.Application.CustomerAccounts;

namespace DKH.CustomerService.Application.DeliveryContacts;

public sealed record DeliveryContactActor(CustomerAccountIdentity Identity, Guid StorefrontId);
