using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.CustomerService.Domain.ValueObjects;
using DKH.Platform.Domain.Entities.Auditing;
using DKH.Platform.Domain.Events;
using DKH.Platform.MultiTenancy;

namespace DKH.CustomerService.Domain.Entities.CustomerProfile;

public sealed class CustomerProfileEntity : FullAuditedEntityWithKey<Guid>,
    IAggregateRoot,
    IStorefrontScoped
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<CustomerAddressEntity> _addresses = [];
    private readonly List<WishlistItemEntity> _wishlistItems = [];

    private CustomerProfileEntity()
    {
        Id = Guid.Empty;
        TelegramUserId = string.Empty;
        FirstName = string.Empty;
        LanguageCode = "en";
        CreationTime = DateTime.UtcNow;
        AccountStatus = AccountStatus.CreateNew();
        ContactVerification = ContactVerification.CreateNew();
        Preferences = CustomerPreferences.CreateDefault();
    }

    private CustomerProfileEntity(
        Guid storefrontId,
        string telegramUserId,
        string firstName,
        string? lastName,
        string? username,
        string? photoUrl,
        string languageCode)
        : base(Guid.NewGuid())
    {
        StorefrontId = storefrontId;
        TelegramUserId = Require(telegramUserId, nameof(telegramUserId));
        FirstName = Require(firstName, nameof(firstName));
        LastName = lastName;
        Username = username;
        PhotoUrl = photoUrl;
        LanguageCode = languageCode ?? "en";
        CreationTime = DateTime.UtcNow;
        AccountStatus = AccountStatus.CreateNew();
        ContactVerification = ContactVerification.CreateNew();
        Preferences = CustomerPreferences.CreateDefault();
    }

    public Guid StorefrontId { get; private set; }

    public string TelegramUserId { get; private set; }

    public string FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string? Username { get; private set; }

    public string? PhotoUrl { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string LanguageCode { get; private set; }

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
        string telegramUserId,
        string firstName,
        string? lastName = null,
        string? username = null,
        string? photoUrl = null,
        string? languageCode = null)
    {
        return new CustomerProfileEntity(storefrontId, telegramUserId, firstName, lastName, username, photoUrl, languageCode ?? "en");
    }

    public void Update(
        string? firstName = null,
        string? lastName = null,
        string? phone = null,
        string? email = null,
        string? languageCode = null)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            FirstName = firstName;
        }

        if (lastName is not null)
        {
            LastName = lastName;
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

        LastModificationTime = DateTime.UtcNow;
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

        LastModificationTime = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletionTime = DateTime.UtcNow;
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
