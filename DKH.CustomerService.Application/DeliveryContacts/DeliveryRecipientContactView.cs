namespace DKH.CustomerService.Application.DeliveryContacts;

public sealed record DeliveryRecipientContactView(DeliveryContactAccessView Access, string RecipientName,
    string Country, string City, string? Province, string? Street, string? Building,
    string? Apartment, string? PostalCode, string? Phone);
