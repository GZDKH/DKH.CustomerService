using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;

namespace DKH.CustomerService.Application.Profiles.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    Guid StorefrontId,
    string UserId,
    string? FirstName,
    string? LastName,
    string? Username,
    string? Phone,
    string? Email,
    string? LanguageCode,
    string? PhotoUrl,
    bool? IsPremium)
    : IRequest<UpdateCustomerResponse>;
