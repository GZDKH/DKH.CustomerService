using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using DKH.Platform.Domain.Enums;

namespace DKH.CustomerService.Application.Addresses.ListAddresses;

public sealed record ListAddressesQuery(
    Guid StorefrontId,
    string UserId,
    PlatformSoftDeleteFilter SoftDeleteFilter = PlatformSoftDeleteFilter.ActiveOnly)
    : IRequest<ListAddressesResponse>;
