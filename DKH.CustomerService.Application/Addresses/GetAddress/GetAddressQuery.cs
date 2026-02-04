using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.GetAddress;

public sealed record GetAddressQuery(Guid StorefrontId, string TelegramUserId, Guid AddressId)
    : IRequest<GetAddressResponse>;
