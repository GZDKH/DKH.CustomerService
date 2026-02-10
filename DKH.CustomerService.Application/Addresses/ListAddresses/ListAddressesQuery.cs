using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Addresses.ListAddresses;

public sealed record ListAddressesQuery(Guid StorefrontId, string TelegramUserId)
    : IRequest<ListAddressesResponse>;
