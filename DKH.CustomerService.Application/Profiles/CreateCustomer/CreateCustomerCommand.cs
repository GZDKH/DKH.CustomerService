using DKH.CustomerService.Contracts.Services.V1;

namespace DKH.CustomerService.Application.Profiles.CreateCustomer;

public sealed record CreateCustomerCommand(
    Guid StorefrontId,
    string TelegramUserId,
    string FirstName,
    string LastName,
    string? Username,
    string? Phone,
    string? Email,
    string? LanguageCode,
    string? PhotoUrl,
    bool IsPremium)
    : IRequest<CreateCustomerResponse>;
