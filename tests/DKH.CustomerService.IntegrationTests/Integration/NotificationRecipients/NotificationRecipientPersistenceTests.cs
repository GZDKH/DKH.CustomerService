using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Application.NotificationRecipients;
using DKH.CustomerService.Contracts.Customer.Api.NotificationRecipient.v2;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.IntegrationTests.Integration.Journal;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using EmailState = DKH.CustomerService.Contracts.Customer.Api.NotificationRecipient.v2.NotificationEmailEligibility;

namespace DKH.CustomerService.IntegrationTests.Integration.NotificationRecipients;

/// <summary>Actual isolated PostgreSQL and HTTP/2 generated-client calls; all owner/recipient identities are synthetic.
/// No provider transport is started and no real notification is sent.</summary>
[Trait("Category", "Integration")]
public sealed class NotificationRecipientPersistenceTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    [Fact]
    public async Task SignedNotificationReadsActualPrimaryAndAliasWithoutProvisioningOrChangingHumanAdmissionAsync()
    {
        var seed = await SeedAsync();
        var alias = "notice-alias:" + Guid.NewGuid().ToString("N");
        await fixture.ChangeAsync(db => db.CustomerAccounts.Single(row => row.Id == seed.Account).LinkIdentity(
            JournalHostFixture.Issuer, alias, "oidc", null, DateTime.UtcNow.AddMinutes(-1)));
        var before = await CountsAsync();
        var first = await ReadAsync(seed);
        Assert.Equal(seed.Profile, first.CustomerProfileId.ToGuid());
        Assert.Equal(seed.Storefront, first.StorefrontId.ToGuid());
        Assert.Equal(seed.Identity.Subject, first.CanonicalIdentitySubject);
        Assert.Equal(JournalHostFixture.Issuer, first.CanonicalIdentityIssuer);
        Assert.Equal("same-fixture-email@example.invalid", first.Email);
        Assert.Equal(EmailState.Available, first.EmailEligibility);
        Assert.NotNull(first.EmailVerifiedAt);
        Assert.True(first.TelegramNotificationsEnabled);
        Assert.Equal(first, NotificationRecipientModel.Parser.ParseFrom(first.ToByteArray()));
        Assert.InRange(first.ReadValidUntil.ToDateTimeOffset() - first.ObservedAt.ToDateTimeOffset(), TimeSpan.Zero, TimeSpan.FromSeconds(5));
        Assert.Equal(13, NotificationRecipientModel.Descriptor.Fields.InFieldNumberOrder().Count);
        var linked = await ReadAsync(seed with { Identity = new(JournalHostFixture.Issuer, alias) });
        Assert.Equal(first.Email, linked.Email);
        Assert.Equal(first.CanonicalIdentitySubject, linked.CanonicalIdentitySubject);
        var external = await ReadAsync(seed with { Identity = seed.Identity with { Issuer = JournalHostFixture.ExternalIssuer } });
        Assert.Equal(first.CanonicalIdentityIssuer, external.CanonicalIdentityIssuer);
        Assert.Equal(before, await CountsAsync());
        var denied = await Assert.ThrowsAsync<RpcException>(() => fixture.ResolveAsync(ServiceToken()));
        Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("human")]
    [InlineData("other-client")]
    [InlineData("wrong-purpose")]
    [InlineData("expired")]
    [InlineData("unsigned")]
    [InlineData("duplicate-client")]
    public async Task OtherOrInvalidCallersCannotReadPrivateRecipientAsync(string kind)
    {
        var seed = await SeedAsync();
        var token = kind switch
        {
            "missing" => null,
            "human" => fixture.Token(seed.Identity.Subject),
            "other-client" => ServiceToken(client: "dkh-order-service"),
            "wrong-purpose" => ServiceToken(purpose: "personal:v1"),
            "expired" => ServiceToken(expired: true),
            "duplicate-client" => ServiceToken(extra: ",\"azp\":\"dkh-notification-service\""),
            _ => ServiceToken()
        };
        if (kind == "unsigned")
        {
            var signed = token!;
            token = signed[..(signed.LastIndexOf('.') + 1)] + "unsigned";
        }

        var failure = await Assert.ThrowsAsync<RpcException>(() => ReadAsync(seed, token: token, useDefaultToken: false));
        Assert.True(failure.StatusCode is StatusCode.PermissionDenied or StatusCode.Unauthenticated);
    }

    [Theory]
    [InlineData("foreign-profile")]
    [InlineData("foreign-storefront")]
    [InlineData("absent-profile")]
    [InlineData("membership")]
    [InlineData("account")]
    [InlineData("profile")]
    public async Task ForeignOrRevokedNativeSourcesCannotExposeContactAsync(string kind)
    {
        var seed = await SeedAsync();
        if (kind == "foreign-profile")
        {
            seed = seed with { Profile = (await SeedAsync()).Profile };
        }
        else if (kind == "foreign-storefront")
        {
            seed = seed with { Storefront = Guid.NewGuid() };
        }
        else if (kind == "absent-profile")
        {
            seed = seed with { Profile = Guid.NewGuid() };
        }
        else
        {
            await fixture.ChangeAsync(db =>
            {
                if (kind == "membership")
                {
                    db.StorefrontMemberships.Single(row => row.Id == seed.Membership).Revoke();
                }
                else if (kind == "account")
                {
                    db.CustomerAccounts.Single(row => row.Id == seed.Account).Block();
                }
                else
                {
                    db.CustomerProfiles.Single(row => row.Id == seed.Profile).SoftDelete();
                }
            });
        }

        var failure = await Assert.ThrowsAsync<RpcException>(() => ReadAsync(seed));
        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("legacy-verified-override")]
    [InlineData("matching-profile")]
    [InlineData("changed-primary")]
    public async Task ActualPreferenceAndCanonicalVerificationControlDestinationWithoutLegacyFlagFallbackAsync(string kind)
    {
        var seed = await SeedAsync();
        await fixture.ChangeAsync(db =>
        {
            var profile = db.CustomerProfiles.Single(row => row.Id == seed.Profile);
            if (kind == "disabled")
            {
                profile.Preferences.UpdateNotificationChannels(false, false, null);
            }
            else if (kind == "legacy-verified-override")
            {
                profile.Update(email: "unproven-override@example.invalid");
                profile.ContactVerification.VerifyEmail();
            }
            else if (kind == "matching-profile")
            {
                profile.Update(email: "same-fixture-email@example.invalid");
            }
            else
            {
                db.CustomerAccounts.Single(row => row.Id == seed.Account)
                    .UpdateVerifiedEmail("changed-verified@example.invalid", DateTime.UtcNow.AddSeconds(-1));
            }
        });
        var result = await ReadAsync(seed);
        if (kind is "disabled" or "legacy-verified-override")
        {
            Assert.Equal(string.Empty, result.Email);
            Assert.Null(result.EmailVerifiedAt);
            Assert.Equal(kind == "disabled" ? EmailState.Disabled : EmailState.UnverifiedProfileOverride, result.EmailEligibility);
        }
        else
        {
            Assert.Equal(EmailState.Available, result.EmailEligibility);
            Assert.Equal(kind == "changed-primary" ? "changed-verified@example.invalid" : "same-fixture-email@example.invalid", result.Email);
        }
    }

    [Theory]
    [InlineData("profile-data")]
    [InlineData("alias")]
    [InlineData("account")]
    [InlineData("membership")]
    [InlineData("time")]
    public async Task RevocationOrChangedDataDuringNativeAwaitCannotReturnEarlierContactAsync(string kind)
    {
        var seed = await SeedAsync();
        var alias = "revocable-notice-alias:" + Guid.NewGuid().ToString("N");
        if (kind == "alias")
        {
            await fixture.ChangeAsync(db => db.CustomerAccounts.Single(row => row.Id == seed.Account).LinkIdentity(
                JournalHostFixture.Issuer, alias, "oidc", null, DateTime.UtcNow.AddMinutes(-1)));
            seed = seed with { Identity = new(JournalHostFixture.Issuer, alias) };
        }

        var clock = new NoticeClock();
        var calls = 0;
        await fixture.ReadAsync(async db =>
        {
            var reader = new CurrentNotificationRecipient(db, new(db, new(db)), clock);
            async Task Current(CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                if (++calls != 3)
                {
                    return;
                }

                if (kind == "time")
                {
                    clock.Advance();
                    return;
                }

                await fixture.ChangeAsync(writer =>
                {
                    if (kind == "profile-data")
                    {
                        writer.CustomerProfiles.Single(row => row.Id == seed.Profile).Preferences.UpdateNotificationChannels(false, null, null);
                    }
                    else if (kind == "alias")
                    {
                        writer.LinkedCustomerIdentities.Remove(writer.LinkedCustomerIdentities.Single(row => row.ProviderSubject == alias));
                    }
                    else if (kind == "membership")
                    {
                        writer.StorefrontMemberships.Single(row => row.Id == seed.Membership).Revoke();
                    }
                    else
                    {
                        writer.CustomerAccounts.Single(row => row.Id == seed.Account).Block();
                    }
                });
            }

            var error = await Record.ExceptionAsync(() => reader.ReadAsync(seed.Identity, seed.Storefront, seed.Profile,
                clock.GetUtcNow().AddMinutes(1), Current, default));
            Assert.True(error is CustomerAccountNotFoundException or CustomerAccountAccessException or NotificationRecipientChangedException);
            Assert.False(db.ChangeTracker.HasChanges());
            return true;
        });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("wrong-header")]
    [InlineData("duplicate-header")]
    public async Task AmbiguousOrInvalidRecipientSelectorsAreRejectedAsync(string kind)
    {
        var seed = await SeedAsync();
        var request = Request(seed);
        var headers = Headers(ServiceToken());
        if (kind == "missing")
        {
            request.CustomerProfileId = null;
        }
        else if (kind == "malformed")
        {
            request.CustomerProfileId = new() { Value = "not-a-guid" };
        }
        else
        {
            if (kind == "duplicate-header")
            {
                headers.Add("x-storefront-id", seed.Storefront.ToString("D"));
                headers.Add("x-storefront-id", seed.Storefront.ToString("D"));
            }
            else
            {
                headers.Add("x-storefront-id", Guid.NewGuid().ToString("D"));
            }
        }

        var failure = await Assert.ThrowsAsync<RpcException>(async () => await Client.GetBudgetChangeRecipientAsync(request, headers));
        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    private NotificationRecipientService.NotificationRecipientServiceClient Client => new(fixture.Channel);
    private async Task<NotificationRecipientModel> ReadAsync(Seed seed, string? token = null, bool useDefaultToken = true)
        => await Client.GetBudgetChangeRecipientAsync(Request(seed), Headers(useDefaultToken ? ServiceToken() : token));
    private static GetBudgetChangeRecipientRequest Request(Seed seed) => new()
    {
        StorefrontId = GuidValue.FromGuid(seed.Storefront),
        CustomerProfileId = GuidValue.FromGuid(seed.Profile),
        OriginalActorIssuer = seed.Identity.Issuer,
        OriginalActorSubject = seed.Identity.Subject
    };
    private static Metadata Headers(string? token) => token is null ? [] : new Metadata { { "authorization", "Bearer " + token } };
    private string ServiceToken(string client = "dkh-notification-service", string purpose = "service:v1", bool expired = false, string extra = "")
        => fixture.Token("synthetic-notification-machine", purpose: System.Text.Json.JsonSerializer.Serialize(purpose), expired: expired,
            extra: ",\"azp\":" + System.Text.Json.JsonSerializer.Serialize(client) + ",\"preferred_username\":" +
                System.Text.Json.JsonSerializer.Serialize("service-account-" + client) + extra);
    private async Task<string> CountsAsync() => await fixture.ReadAsync(async db =>
        $"{await db.CustomerAccounts.CountAsync()}:{await db.CustomerProfiles.CountAsync()}:{await db.StorefrontMemberships.CountAsync()}:{await db.LinkedCustomerIdentities.CountAsync()}");
    private async Task<Seed> SeedAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var now = DateTime.UtcNow.AddMinutes(-1);
        var profile = CustomerProfileEntity.Create(Guid.NewGuid(), "legacy-not-a-keycloak-id:" + Guid.NewGuid().ToString("N"), "Synthetic");
        profile.BeginAccountReconciliation(now);
        profile.CompleteAccountReconciliation(account.Id, now);
        var membership = StorefrontMembershipEntity.Create(account.Id, profile.StorefrontId, now, profile.Id);
        await fixture.ChangeAsync(db => db.AddRange(profile, membership));
        return new(account.Id, profile.Id, membership.Id, profile.StorefrontId, new(JournalHostFixture.Issuer, account.IdentitySubject));
    }
    private sealed record Seed(Guid Account, Guid Profile, Guid Membership, Guid Storefront, CustomerAccountIdentity Identity);
    private sealed class NoticeClock : TimeProvider
    {
        private DateTimeOffset _at = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _at;
        public void Advance() => _at = _at.AddSeconds(6);
    }
}
