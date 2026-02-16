using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.CustomerService.Domain.ValueObjects;
using DKH.Platform.Domain.Entities.Auditing;
using DKH.Platform.Domain.Events;
using DKH.Platform.MultiTenancy;

namespace DKH.CustomerService.Domain.Entities.CustomerProfile;

public sealed class CustomerProfileEntity : FullAuditedEntityWithKey<Guid>,
    IAggregateRoot,
    IPlatformStorefrontScoped
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<CustomerAddressEntity> _addresses = [];
    private readonly List<WishlistItemEntity> _wishlistItems = [];

    private CustomerProfileEntity()
    {
        Id = Guid.Empty;
        UserId = string.Empty;
        ProviderType = "Telegram";
        FirstName = string.Empty;
        LanguageCode = "en";
        AccountStatus = AccountStatus.CreateNew();
        ContactVerification = ContactVerification.CreateNew();
        Preferences = CustomerPreferences.CreateDefault();
    }

    private CustomerProfileEntity(
        Guid storefrontId,
        string userId,
        string firstName,
        string? lastName,
        string? username,
        string? photoUrl,
        string? phone,
        string? email,
        string languageCode,
        bool isPremium,
        string providerType)
        : base(Guid.NewGuid())
    {
        StorefrontId = storefrontId;
        UserId = Require(userId, nameof(userId));
        ProviderType = providerType;
        FirstName = Require(firstName, nameof(firstName));
        LastName = lastName;
        Username = username;
        PhotoUrl = photoUrl;
        Phone = phone;
        Email = email;
        LanguageCode = languageCode ?? "en";
        IsPremium = isPremium;
        AccountStatus = AccountStatus.CreateNew();
        ContactVerification = ContactVerification.CreateNew();
        Preferences = CustomerPreferences.CreateDefault();
    }

    public Guid StorefrontId { get; private set; }

    public string UserId { get; private set; }

    public string ProviderType { get; private set; }

    public string FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string? Username { get; private set; }

    public string? PhotoUrl { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string LanguageCode { get; private set; }

    public bool IsPremium { get; private set; }

    public AccountStatus AccountStatus { get; private set; }

    public ContactVerification ContactVerification { get; private set; }

    public CustomerPreferences Preferences { get; private set; }

    public IReadOnlyCollection<CustomerAddressEntity> Addresses => _addresses.AsReadOnly();

    public IReadOnlyCollection<WishlistItemEntity> WishlistItems => _wishlistItems.AsReadOnly();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    public override object?[] GetKeys() => [Id];

    public static CustomerProfileEntity Create(
        Guid storefrontId,
        string userId,
        string firstName,
        string? lastName = null,
        string? username = null,
        string? photoUrl = null,
        string? phone = null,
        string? email = null,
        string? languageCode = null,
        bool isPremium = false,
        string providerType = "Telegram")
    {
        return new CustomerProfileEntity(storefrontId, userId, firstName, lastName, username, photoUrl, phone, email, languageCode ?? "en", isPremium, providerType);
    }

    public void Update(
        string? firstName = null,
        string? lastName = null,
        string? username = null,
        string? phone = null,
        string? email = null,
        string? languageCode = null,
        string? photoUrl = null,
        bool? isPremium = null)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            FirstName = firstName;
        }

        if (lastName is not null)
        {
            LastName = lastName;
        }

        if (username is not null)
        {
            Username = username;
        }

        if (phone is not null)
        {
            if (Phone != phone)
            {
                ContactVerification.ResetPhoneVerification();
            }

            Phone = phone;
        }

        if (email is not null)
        {
            if (Email != email)
            {
                ContactVerification.ResetEmailVerification();
            }

            Email = email;
        }

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            LanguageCode = languageCode;
            Preferences.UpdateLanguage(languageCode);
        }

        if (photoUrl is not null)
        {
            PhotoUrl = photoUrl;
        }

        if (isPremium.HasValue)
        {
            IsPremium = isPremium.Value;
        }
    }

    public void UpdateFromTelegram(string firstName, string? lastName, string? username, string? photoUrl, string? languageCode)
    {
        FirstName = Require(firstName, nameof(firstName));
        LastName = lastName;
        Username = username;
        PhotoUrl = photoUrl;
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            LanguageCode = languageCode;
        }
    }

    public void SoftDelete()
    {
        MarkAsDeleted();
        AccountStatus.MarkDeleted();
    }

    public void Anonymize()
    {
        FirstName = "Deleted";
        LastName = "User";
        Username = null;
        PhotoUrl = null;
        Phone = null;
        Email = null;
        SoftDelete();
    }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} must be provided", name);
        }

        return value;
    }

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
