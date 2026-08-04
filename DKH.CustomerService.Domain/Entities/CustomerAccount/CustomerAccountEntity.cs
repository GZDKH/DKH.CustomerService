using System.Net.Mail;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Domain.Events;
using DKH.Platform.Domain.Entities.Auditing;
using DKH.Platform.Domain.Events;

namespace DKH.CustomerService.Domain.Entities.CustomerAccount;

public sealed class CustomerAccountEntity : FullAuditedEntityWithKey<Guid>, IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<LinkedCustomerIdentityEntity> _linkedIdentities = [];

    private CustomerAccountEntity()
    {
        Id = Guid.Empty;
        IdentityIssuer = string.Empty;
        IdentitySubject = string.Empty;
        VerifiedEmail = string.Empty;
        PreferredLocale = "en";
        Status = CustomerAccountStatusType.Active;
    }

    private CustomerAccountEntity(
        string identityIssuer,
        string identitySubject,
        string verifiedEmail,
        string? firstName,
        string? lastName,
        string preferredLocale,
        DateTime emailVerifiedAt)
        : base(Guid.NewGuid())
    {
        IdentityIssuer = NormalizeIdentityIssuer(identityIssuer);
        IdentitySubject = NormalizeIdentitySubject(identitySubject);
        VerifiedEmail = NormalizeEmail(verifiedEmail);
        FirstName = NormalizeOptional(firstName);
        LastName = NormalizeOptional(lastName);
        PreferredLocale = NormalizeLocale(preferredLocale);
        EmailVerifiedAt = EnsureUtc(emailVerifiedAt);
        Status = CustomerAccountStatusType.Active;
    }

    public string IdentityIssuer { get; private set; }

    public string IdentitySubject { get; private set; }

    public string VerifiedEmail { get; private set; }

    public DateTime EmailVerifiedAt { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string PreferredLocale { get; private set; }

    public CustomerAccountStatusType Status { get; private set; }

    public IReadOnlyCollection<LinkedCustomerIdentityEntity> LinkedIdentities => _linkedIdentities.AsReadOnly();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public override object?[] GetKeys() => [Id];

    public static CustomerAccountEntity Create(
        string identityIssuer,
        string identitySubject,
        string verifiedEmail,
        string? firstName,
        string? lastName,
        string? preferredLocale,
        DateTime emailVerifiedAt)
    {
        var account = new CustomerAccountEntity(
            identityIssuer,
            identitySubject,
            verifiedEmail,
            firstName,
            lastName,
            preferredLocale ?? "en",
            emailVerifiedAt);

        account._domainEvents.Add(new CustomerAccountCreatedDomainEvent(account.Id));
        return account;
    }

    public void UpdateVerifiedEmail(string verifiedEmail, DateTime verifiedAt)
    {
        VerifiedEmail = NormalizeEmail(verifiedEmail);
        EmailVerifiedAt = EnsureUtc(verifiedAt);
    }

    public void UpdateProfile(string? firstName, string? lastName, string? preferredLocale)
    {
        FirstName = NormalizeOptional(firstName);
        LastName = NormalizeOptional(lastName);
        PreferredLocale = NormalizeLocale(preferredLocale ?? PreferredLocale);
    }

    public LinkedCustomerIdentityEntity LinkIdentity(
        string providerAuthority,
        string providerSubject,
        string providerKind,
        string? displayName,
        DateTime verifiedAt,
        Guid? legacyExternalIdentityId = null)
    {
        var normalizedAuthority = NormalizeLinkedProviderAuthority(providerAuthority);
        var normalizedSubject = NormalizeIdentitySubject(providerSubject);
        var normalizedKind = NormalizeLinkedProviderKind(providerKind);

        if (_linkedIdentities.Any(identity =>
                identity.ProviderAuthority == normalizedAuthority &&
                identity.ProviderSubject == normalizedSubject))
        {
            throw new InvalidOperationException("The provider identity is already linked to this account.");
        }

        var identity = LinkedCustomerIdentityEntity.Create(
            Id,
            normalizedAuthority,
            normalizedSubject,
            normalizedKind,
            displayName,
            verifiedAt,
            legacyExternalIdentityId);

        _linkedIdentities.Add(identity);
        return identity;
    }

    public bool UnlinkIdentity(Guid linkedIdentityId)
    {
        var identity = _linkedIdentities.SingleOrDefault(item => item.Id == linkedIdentityId);
        return identity is not null && _linkedIdentities.Remove(identity);
    }

    public void Block() => Status = CustomerAccountStatusType.Blocked;

    public void Unblock()
    {
        if (Status == CustomerAccountStatusType.DeletionPending)
        {
            throw new InvalidOperationException("An account pending deletion cannot be unblocked.");
        }

        Status = CustomerAccountStatusType.Active;
    }

    public void MarkDeletionPending() => Status = CustomerAccountStatusType.DeletionPending;

    public void AnonymizeForDeletion(DateTime deletedAt)
    {
        IdentityIssuer = "https://deleted.invalid";
        IdentitySubject = $"deleted:{Id:N}";
        VerifiedEmail = $"deleted-{Id:N}@invalid.local";
        EmailVerifiedAt = EnsureUtc(deletedAt);
        FirstName = null;
        LastName = null;
        PreferredLocale = "en";
        Status = CustomerAccountStatusType.DeletionPending;

        foreach (var identity in _linkedIdentities)
        {
            identity.AnonymizeForAccountDeletion();
        }

        MarkAsDeleted();
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static string NormalizeIdentityIssuer(string value)
        => NormalizeAuthority(value, nameof(value), 512);

    public static string NormalizeIdentitySubject(string value)
        => Require(value, nameof(value), 256);

    public static string NormalizeLinkedProviderAuthority(string value)
    {
        var authority = Require(value, nameof(value), 512);
        return Uri.TryCreate(authority, UriKind.Absolute, out _)
            ? NormalizeAuthority(authority, nameof(value), 512)
            : authority.ToLowerInvariant();
    }

    public static string NormalizeLinkedProviderKind(string value)
        => Require(value, nameof(value), 32).ToLowerInvariant();

    private static string NormalizeEmail(string value)
    {
        var email = Require(value, nameof(value), 256).ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out _))
        {
            throw new ArgumentException("A valid verified email address is required.", nameof(value));
        }

        return email;
    }

    private static string NormalizeAuthority(string value, string parameterName, int maximumLength)
    {
        var authority = Require(value, parameterName, maximumLength);
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("An absolute HTTP(S) identity issuer is required.", parameterName);
        }

        var normalized = uri.AbsoluteUri.TrimEnd('/');
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{parameterName} must not exceed {maximumLength} characters.", parameterName);
    }

    private static string NormalizeLocale(string value)
        => Require(value, nameof(value), 16).ToLowerInvariant();

    private static string Require(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} must be provided.", parameterName);
        }

        var normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{parameterName} must not exceed {maximumLength} characters.", parameterName);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= 100
            ? normalized
            : throw new ArgumentException("Profile names must not exceed 100 characters.", nameof(value));
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
