using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ExternalIdentity;

public sealed class CustomerExternalIdentityEntity : FullAuditedEntityWithKey<Guid>
{
    private CustomerExternalIdentityEntity()
    {
        Id = Guid.Empty;
        CustomerId = Guid.Empty;
        Provider = string.Empty;
        ProviderUserId = string.Empty;
    }

    private CustomerExternalIdentityEntity(
        Guid customerId,
        string provider,
        string providerUserId,
        string? email,
        string? displayName,
        bool isPrimary)
        : base(Guid.NewGuid())
    {
        CustomerId = customerId;
        Provider = Require(provider, nameof(provider));
        ProviderUserId = Require(providerUserId, nameof(providerUserId));
        Email = email;
        DisplayName = displayName;
        IsPrimary = isPrimary;
        LinkedAt = DateTime.UtcNow;
    }

    public Guid CustomerId { get; private set; }

    public string Provider { get; private set; }

    public string ProviderUserId { get; private set; }

    public string? Email { get; private set; }

    public string? DisplayName { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTime LinkedAt { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static CustomerExternalIdentityEntity Create(
        Guid customerId,
        string provider,
        string providerUserId,
        string? email = null,
        string? displayName = null,
        bool isPrimary = false)
    {
        return new CustomerExternalIdentityEntity(customerId, provider, providerUserId, email, displayName, isPrimary);
    }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    public void UpdateEmail(string? email)
    {
        Email = email;
    }

    public void UpdateDisplayName(string? displayName)
    {
        DisplayName = displayName;
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
