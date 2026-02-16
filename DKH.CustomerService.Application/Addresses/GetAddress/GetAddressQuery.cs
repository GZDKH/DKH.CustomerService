using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.GetAddress;

public sealed record GetAddressQuery(Guid StorefrontId, string UserId, Guid AddressId)
    : IRequest<GetAddressResponse>;
