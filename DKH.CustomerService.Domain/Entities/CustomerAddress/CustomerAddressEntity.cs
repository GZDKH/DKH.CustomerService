using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.CustomerAddress;

public sealed class CustomerAddressEntity : FullAuditedEntityWithKey<Guid>
{
    private CustomerAddressEntity()
    {
        Id = Guid.Empty;
        CustomerId = Guid.Empty;
        Label = string.Empty;
        Country = string.Empty;
        City = string.Empty;
        CreationTime = DateTime.UtcNow;
    }

    private CustomerAddressEntity(
        Guid customerId,
        string label,
        string country,
        string city,
        string? street,
        string? building,
        string? apartment,
        string? postalCode,
        string? phone,
        bool isDefault)
        : base(Guid.NewGuid())
    {
        CustomerId = customerId;
        Label = Require(label, nameof(label));
        Country = Require(country, nameof(country));
        City = Require(city, nameof(city));
        Street = street;
        Building = building;
        Apartment = apartment;
        PostalCode = postalCode;
        Phone = phone;
        IsDefault = isDefault;
        CreationTime = DateTime.UtcNow;
    }

    public Guid CustomerId { get; private set; }

    public string Label { get; private set; }

    public string Country { get; private set; }

    public string City { get; private set; }

    public string? Street { get; private set; }

    public string? Building { get; private set; }

    public string? Apartment { get; private set; }

    public string? PostalCode { get; private set; }

    public string? Phone { get; private set; }

    public bool IsDefault { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static CustomerAddressEntity Create(
        Guid customerId,
        string label,
        string country,
        string city,
        string? street = null,
        string? building = null,
        string? apartment = null,
        string? postalCode = null,
        string? phone = null,
        bool isDefault = false)
    {
        return new CustomerAddressEntity(customerId, label, country, city, street, building, apartment, postalCode, phone, isDefault);
    }

    public void Update(
        string? label = null,
        string? country = null,
        string? city = null,
        string? street = null,
        string? building = null,
        string? apartment = null,
        string? postalCode = null,
        string? phone = null)
    {
        if (!string.IsNullOrWhiteSpace(label))
        {
            Label = label;
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            Country = country;
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            City = city;
        }

        if (street is not null)
        {
            Street = street;
        }

        if (building is not null)
        {
            Building = building;
        }

        if (apartment is not null)
        {
            Apartment = apartment;
        }

        if (postalCode is not null)
        {
            PostalCode = postalCode;
        }

        if (phone is not null)
        {
            Phone = phone;
        }

        LastModificationTime = DateTime.UtcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        LastModificationTime = DateTime.UtcNow;
    }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} must be provided", name);
        }

        return value;
    }
}
