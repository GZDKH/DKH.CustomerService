using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.CreateAddress;

public sealed record CreateAddressCommand(
    Guid StorefrontId,
    string TelegramUserId,
    string Label,
    string Country,
    string City,
    string? Street,
    string? Building,
    string? Apartment,
    string? PostalCode,
    string? Phone,
    bool IsDefault)
    : IRequest<CreateAddressResponse>;
