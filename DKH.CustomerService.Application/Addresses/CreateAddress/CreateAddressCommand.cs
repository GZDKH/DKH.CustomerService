using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.CreateAddress;

public sealed record CreateAddressCommand(
    Guid StorefrontId,
    string UserId,
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
