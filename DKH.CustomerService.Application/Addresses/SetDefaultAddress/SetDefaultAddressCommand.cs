using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.SetDefaultAddress;

public sealed record SetDefaultAddressCommand(Guid StorefrontId, string UserId, Guid AddressId)
    : IRequest<SetDefaultAddressResponse>;
