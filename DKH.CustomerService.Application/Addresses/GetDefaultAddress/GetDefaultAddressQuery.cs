using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Addresses.GetDefaultAddress;

public sealed record GetDefaultAddressQuery(Guid StorefrontId, string UserId)
    : IRequest<GetDefaultAddressResponse>;
