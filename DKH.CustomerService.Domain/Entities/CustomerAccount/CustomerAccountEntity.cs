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
        IdentityIssuer = NormalizeAuthority(identityIssuer, nameof(identityIssuer));
        IdentitySubject = Require(identitySubject, nameof(identitySubject));
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
        DateTime verifiedAt)
    {
        var normalizedAuthority = NormalizeProviderAuthority(providerAuthority);
        var normalizedSubject = Require(providerSubject, nameof(providerSubject));
        var normalizedKind = Require(providerKind, nameof(providerKind)).ToLowerInvariant();

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
            verifiedAt);

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

    public void ClearDomainEvents() => _domainEvents.Clear();

    private static string NormalizeEmail(string value)
    {
        var email = Require(value, nameof(value)).ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out _))
        {
            throw new ArgumentException("A valid verified email address is required.", nameof(value));
        }

        return email;
    }

    private static string NormalizeAuthority(string value, string parameterName)
    {
        var authority = Require(value, parameterName);
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("An absolute HTTP(S) identity issuer is required.", parameterName);
        }

        return uri.AbsoluteUri.TrimEnd('/');
    }

    private static string NormalizeProviderAuthority(string value)
    {
        var authority = Require(value, nameof(value));
        return Uri.TryCreate(authority, UriKind.Absolute, out _)
            ? NormalizeAuthority(authority, nameof(value))
            : authority.ToLowerInvariant();
    }

    private static string NormalizeLocale(string value)
        => Require(value, nameof(value)).ToLowerInvariant();

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} must be provided.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
