using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.DeleteAddress;

public sealed record DeleteAddressCommand(Guid StorefrontId, string TelegramUserId, Guid AddressId)
    : IRequest<DeleteAddressResponse>;
