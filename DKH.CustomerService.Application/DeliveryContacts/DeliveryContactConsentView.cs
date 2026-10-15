namespace DKH.CustomerService.Application.DeliveryContacts;

public sealed record DeliveryContactConsentView(Guid Id, long Revision, DateTimeOffset ValidUntilUtc, bool Revoked);
