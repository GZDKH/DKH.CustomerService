namespace DKH.CustomerService.Application.IntegrationEvents.Payloads;

public sealed record CustomerSegmentChangedPayload(
    Guid CustomerId,
    Guid StorefrontId,
    string OldStatus,
    string NewStatus);
