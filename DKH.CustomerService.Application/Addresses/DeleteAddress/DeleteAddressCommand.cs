using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.DeleteAddress;

public sealed record DeleteAddressCommand(Guid StorefrontId, string UserId, Guid AddressId)
    : IRequest<DeleteAddressResponse>;
