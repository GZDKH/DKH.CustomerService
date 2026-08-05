using System.Diagnostics.Metrics;
using DKH.CustomerService.Application.Observability;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class CustomerAccountMetricsTests
{
    [Fact]
    public void RecordMetrics_UsesOnlyBoundedPrivacySafeTags()
    {
        var measurements = new List<Measurement>();
        using var listener = CreateListener(measurements);
        var storefrontId = Guid.NewGuid();

        CustomerAccountMetrics.RecordIdentity(
            CustomerIdentityOutcome.Linked,
            "telegram:raw-provider-subject",
            storefrontId,
            TimeSpan.FromMilliseconds(20));
        CustomerAccountMetrics.RecordMembership(
            StorefrontMembershipOutcome.FirstTouch,
            storefrontId,
            TimeSpan.FromMilliseconds(30));

        measurements.Should().HaveCount(4);
        measurements.SelectMany(measurement => measurement.Tags.Keys)
            .Distinct(StringComparer.Ordinal)
            .Should().BeSubsetOf(["provider_kind", "storefront_id", "outcome"]);
        measurements.SelectMany(measurement => measurement.Tags.Keys)
            .Should().NotContain(key => ForbiddenTagNames.Contains(key));
        measurements.SelectMany(measurement => measurement.Tags.Values)
            .Should().NotContain("telegram:raw-provider-subject");
        measurements.Where(measurement => measurement.Name == "customer.identity")
            .Should().ContainSingle(measurement => measurement.Tags["provider_kind"] == "other");
    }

    [Theory]
    [InlineData("telegram", "telegram")]
    [InlineData("Telegram-Widget", "telegram")]
    [InlineData("magic-link", "email")]
    [InlineData("WEIXIN", "wechat")]
    [InlineData("unbounded-provider-value", "other")]
    [InlineData(null, "other")]
    public void NormalizeProviderKind_ReturnsBoundedValue(string? provider, string expected)
        => CustomerAccountMetrics.NormalizeProviderKind(provider).Should().Be(expected);

    private static MeterListener CreateListener(ICollection<Measurement> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == CustomerAccountMetrics.MeterName)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, ToDictionary(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, ToDictionary(tags))));
        listener.Start();
        return listener;
    }

    private static Dictionary<string, string> ToDictionary(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            result[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }

        return result;
    }

    private static readonly HashSet<string> ForbiddenTagNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email",
        "phone",
        "name",
        "address",
        "subject",
        "provider_subject",
        "keycloak_subject",
        "account_id",
        "customer_id",
        "user_id",
        "token",
        "error",
    };

    private sealed record Measurement(
        string Name,
        double Value,
        IReadOnlyDictionary<string, string> Tags);
}
