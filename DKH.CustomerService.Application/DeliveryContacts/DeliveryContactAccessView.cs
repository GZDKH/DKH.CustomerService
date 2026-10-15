namespace DKH.CustomerService.Application.DeliveryContacts;

public sealed record DeliveryContactAccessView(Guid Id, DateTimeOffset ExpiresAtUtc, long ScopeRevision);
