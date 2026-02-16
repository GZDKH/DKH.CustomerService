using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.GetDefaultAddress;

public sealed record GetDefaultAddressQuery(Guid StorefrontId, string UserId)
    : IRequest<GetDefaultAddressResponse>;
