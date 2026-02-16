using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.UpdateAddress;

public sealed record UpdateAddressCommand(
    Guid StorefrontId,
    string UserId,
    Guid AddressId,
    string? Label,
    string? Country,
    string? City,
    string? Street,
    string? Building,
    string? Apartment,
    string? PostalCode,
    string? Phone)
    : IRequest<UpdateAddressResponse>;
