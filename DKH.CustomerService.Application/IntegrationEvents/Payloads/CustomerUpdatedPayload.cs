namespace DKH.CustomerService.Application.IntegrationEvents.Payloads;

public sealed record CustomerUpdatedPayload(
    Guid CustomerId,
    Guid StorefrontId,
    string UserId);
