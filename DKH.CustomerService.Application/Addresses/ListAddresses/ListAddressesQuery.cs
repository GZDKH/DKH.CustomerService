using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.ListAddresses;

public sealed record ListAddressesQuery(Guid StorefrontId, string TelegramUserId)
    : IRequest<ListAddressesResponse>;
