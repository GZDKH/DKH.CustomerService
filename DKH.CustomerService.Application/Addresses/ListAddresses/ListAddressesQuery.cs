using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.ListAddresses;

public sealed record ListAddressesQuery(Guid StorefrontId, string UserId)
    : IRequest<ListAddressesResponse>;
