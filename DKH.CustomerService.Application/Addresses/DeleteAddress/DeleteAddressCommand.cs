using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Addresses.DeleteAddress;

public sealed record DeleteAddressCommand(Guid StorefrontId, string TelegramUserId, Guid AddressId)
    : IRequest<DeleteAddressResponse>;
