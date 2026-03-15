namespace DKH.CustomerService.Application.IntegrationEvents.Payloads;

public sealed record CustomerCreatedPayload(
    Guid CustomerId,
    Guid StorefrontId,
    string UserId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string LanguageCode);
