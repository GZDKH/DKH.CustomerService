using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DKH.CustomerService.Application.Observability;

public enum CustomerIdentityOutcome
{
    Linked,
    LinkRejected,
    Unlinked,
    RecoveryStarted,
}

public enum StorefrontMembershipOutcome
{
    FirstTouch,
    ReturningTouch,
    Revoked,
}

/// <summary>
/// Privacy-safe unified-account metrics. Provider values are reduced to a bounded kind and no
/// customer, account, Keycloak subject, provider subject, contact, or token is emitted.
/// </summary>
public static class CustomerAccountMetrics
{
    public const string MeterName = "DKH.CustomerService.CustomerAccount";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> IdentityCounter = Meter.CreateCounter<long>("customer.identity");
    private static readonly Histogram<double> IdentityDuration =
        Meter.CreateHistogram<double>("customer.identity.duration", "s");
    private static readonly Counter<long> MembershipCounter = Meter.CreateCounter<long>("customer.membership");
    private static readonly Histogram<double> MembershipDuration =
        Meter.CreateHistogram<double>("customer.membership.duration", "s");

    public static void RecordIdentity(
        CustomerIdentityOutcome outcome,
        string? provider,
        Guid? storefrontId,
        TimeSpan duration)
    {
        TagList tags = new()
        {
            { "provider_kind", NormalizeProviderKind(provider) },
            { "storefront_id", storefrontId?.ToString("D") ?? "unresolved" },
            { "outcome", ToTag(outcome) },
        };
        IdentityCounter.Add(1, tags);
        IdentityDuration.Record(duration.TotalSeconds, tags);
    }

    public static void RecordMembership(
        StorefrontMembershipOutcome outcome,
        Guid storefrontId,
        TimeSpan duration)
    {
        TagList tags = new()
        {
            { "storefront_id", storefrontId.ToString("D") },
            { "outcome", ToTag(outcome) },
        };
        MembershipCounter.Add(1, tags);
        MembershipDuration.Record(duration.TotalSeconds, tags);
    }

    public static string NormalizeProviderKind(string? provider)
    {
        var normalized = provider?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "telegram" or "telegram-widget" => "telegram",
            "email" or "magic-link" or "password" => "email",
            "wechat" or "weixin" => "wechat",
            "google" => "google",
            "apple" => "apple",
            _ => "other",
        };
    }

    private static string ToTag(CustomerIdentityOutcome outcome) => outcome switch
    {
        CustomerIdentityOutcome.Linked => "linked",
        CustomerIdentityOutcome.LinkRejected => "link_rejected",
        CustomerIdentityOutcome.Unlinked => "unlinked",
        CustomerIdentityOutcome.RecoveryStarted => "recovery_started",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static string ToTag(StorefrontMembershipOutcome outcome) => outcome switch
    {
        StorefrontMembershipOutcome.FirstTouch => "first_touch",
        StorefrontMembershipOutcome.ReturningTouch => "returning_touch",
        StorefrontMembershipOutcome.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
