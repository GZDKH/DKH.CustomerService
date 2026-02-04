using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.SetDefaultAddress;

public sealed record SetDefaultAddressCommand(Guid StorefrontId, string TelegramUserId, Guid AddressId)
    : IRequest<SetDefaultAddressResponse>;
