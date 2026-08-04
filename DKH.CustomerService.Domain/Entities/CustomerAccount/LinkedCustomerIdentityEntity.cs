using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.CustomerAccount;

public sealed class LinkedCustomerIdentityEntity : FullAuditedEntityWithKey<Guid>
{
    private LinkedCustomerIdentityEntity()
    {
        Id = Guid.Empty;
        CustomerAccountId = Guid.Empty;
        ProviderAuthority = string.Empty;
        ProviderSubject = string.Empty;
        ProviderKind = string.Empty;
    }

    private LinkedCustomerIdentityEntity(
        Guid customerAccountId,
        string providerAuthority,
        string providerSubject,
        string providerKind,
        string? displayName,
        DateTime verifiedAt)
        : base(Guid.NewGuid())
    {
        CustomerAccountId = customerAccountId;
        ProviderAuthority = providerAuthority;
        ProviderSubject = providerSubject;
        ProviderKind = providerKind;
        DisplayName = NormalizeOptional(displayName);
        LinkedAt = DateTime.UtcNow;
        VerifiedAt = EnsureUtc(verifiedAt);
    }

    public Guid CustomerAccountId { get; private set; }

    public string ProviderAuthority { get; private set; }

    public string ProviderSubject { get; private set; }

    public string ProviderKind { get; private set; }

    public string? DisplayName { get; private set; }

    public DateTime LinkedAt { get; private set; }

    public DateTime VerifiedAt { get; private set; }

    public override object?[] GetKeys() => [Id];

    internal static LinkedCustomerIdentityEntity Create(
        Guid customerAccountId,
        string providerAuthority,
        string providerSubject,
        string providerKind,
        string? displayName,
        DateTime verifiedAt)
        => new(
            customerAccountId,
            providerAuthority,
            providerSubject,
            providerKind,
            displayName,
            verifiedAt);

    public void UpdateDisplayName(string? displayName)
        => DisplayName = NormalizeOptional(displayName);

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
