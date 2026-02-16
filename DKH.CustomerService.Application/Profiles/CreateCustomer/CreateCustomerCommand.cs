using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;

namespace DKH.CustomerService.Application.Profiles.CreateCustomer;

public sealed record CreateCustomerCommand(
    Guid StorefrontId,
    string UserId,
    string FirstName,
    string LastName,
    string? Username,
    string? Phone,
    string? Email,
    string? LanguageCode,
    string? PhotoUrl,
    bool IsPremium,
    string ProviderType = "Telegram")
    : IRequest<CreateCustomerResponse>;
