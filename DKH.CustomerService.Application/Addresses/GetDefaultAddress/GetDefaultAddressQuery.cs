using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.GetDefaultAddress;

public sealed record GetDefaultAddressQuery(Guid StorefrontId, string TelegramUserId)
    : IRequest<GetDefaultAddressResponse>;
