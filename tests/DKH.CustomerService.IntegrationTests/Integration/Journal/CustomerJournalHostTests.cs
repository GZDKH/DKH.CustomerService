using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DKH.CustomerService.Api;
using DKH.CustomerService.Api.Journal;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1;
using DKH.CustomerService.Contracts.Customer.Api.ProductCollection.v1;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform.Authentication.Keycloak;
using DKH.Platform.Authorization;
using DKH.Platform.Grpc;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Identity;
using DKH.Platform.MultiTenancy;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace DKH.CustomerService.IntegrationTests.Integration.Journal;

[Trait("Category", "Integration")]
public sealed class CustomerJournalHostTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    [Fact]
    public async Task NonGuidAndVerifiedLinkedSubjects_ResolveOneAccountAcrossStorefrontsAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var alias = $"linked:{Guid.NewGuid():N}";
        await fixture.ChangeAsync(db =>
        {
            db.CustomerAccounts.Attach(account);
            account.LinkIdentity(JournalHostFixture.Issuer, alias, "oidc", null, DateTime.UtcNow.AddMinutes(-1));
            db.StorefrontMemberships.AddRange(
                StorefrontMembershipEntity.Create(account.Id, Guid.NewGuid(), DateTime.UtcNow),
                StorefrontMembershipEntity.Create(account.Id, Guid.NewGuid(), DateTime.UtcNow));
        });
        var primary = await fixture.ResolveAsync(fixture.Token(account.IdentitySubject));
        var linked = await fixture.ResolveAsync(fixture.Token(alias));
        primary.Value.Should().Be(account.Id.ToString());
        linked.Value.Should().Be(primary.Value);
    }

    [Theory]
    [InlineData("unverified")]
    [InlineData("future")]
    [InlineData("foreign-authority")]
    [InlineData("duplicate-primary")]
    public async Task LinkedIdentity_RequiresCurrentIssuerAndCompletedVerificationAsync(string condition)
    {
        var account = await fixture.SeedAccountAsync();
        var alias = condition == "duplicate-primary" ? account.IdentitySubject : "alias:" + Guid.NewGuid();
        await fixture.ChangeAsync(db =>
        {
            db.CustomerAccounts.Attach(account);
            account.LinkIdentity(condition == "foreign-authority" ? "https://foreign.fixture.invalid/realms/test" : JournalHostFixture.Issuer,
                alias, "oidc", null, condition switch
                {
                    "unverified" => DateTime.UnixEpoch,
                    "future" => DateTime.UtcNow.AddDays(1),
                    _ => DateTime.UtcNow.AddMinutes(-1),
                });
        });
        if (condition == "duplicate-primary")
        {
            (await fixture.ResolveAsync(fixture.Token(alias))).Value.Should().Be(account.Id.ToString());
        }
        else
        {
            await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(alias)), StatusCode.NotFound);
        }
    }

    [Fact]
    public async Task ReusedBearer_RechecksAccountStatusAcrossConcurrentRequestsAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        (await fixture.ResolveAsync(token)).Value.Should().Be(account.Id.ToString());
        await fixture.ChangeAsync(db =>
        {
            db.CustomerAccounts.Attach(account);
            account.Block();
        });
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            DeniedAsync(() => fixture.ResolveAsync(token), StatusCode.PermissionDenied)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("\"service:v1\"")]
    [InlineData("\"unsupported:v1\"")]
    [InlineData("\"personal:v2\"")]
    [InlineData("\"personal:v1 \"")]
    [InlineData("[\"personal:v1\"]")]
    [InlineData(/*lang=json,strict*/ "{\"value\":\"personal:v1\"}")]
    public async Task InvalidPurpose_IsDeniedAfterRealJwtValidationAsync(string? purpose)
    {
        var account = await fixture.SeedAccountAsync();
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(account.IdentitySubject, purpose)), StatusCode.PermissionDenied);
    }

    [Fact]
    public async Task DuplicatePurposeAndSubject_AreNotFlattenedIntoPersonalAccessAsync()
    {
        var account = await fixture.SeedAccountAsync();
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(account.IdentitySubject,
            extra: ",\"dkh_principal_purpose\":\"personal:v1\"")), StatusCode.PermissionDenied);
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(account.IdentitySubject,
            extra: ",\"sub\":" + JsonSerializer.Serialize(account.IdentitySubject))), StatusCode.PermissionDenied);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("\"\"")]
    public async Task NonStringOrEmptySubject_IsDeniedAsync(string subjectJson)
    {
        var token = fixture.Token("unused", subjectJson: subjectJson);
        var error = await Assert.ThrowsAsync<RpcException>(() => fixture.ResolveAsync(token));
        error.StatusCode.Should().BeOneOf(StatusCode.Unauthenticated, StatusCode.PermissionDenied);
    }

    [Fact]
    public async Task LongAndWhitespaceSubject_CannotNormalizeIntoAnotherIdentityAsync()
    {
        var account = await fixture.SeedAccountAsync();
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(new string('x', 257))), StatusCode.PermissionDenied);
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(" " + account.IdentitySubject)), StatusCode.PermissionDenied);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("signature")]
    public async Task InvalidJwt_IsRejectedByExistingPlatformAuthenticationAsync(string defect)
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject,
            issuer: defect == "issuer" ? "https://foreign.fixture.invalid/realms/test" : JournalHostFixture.Issuer,
            audience: defect == "audience" ? "foreign-backend" : JournalHostFixture.Audience,
            expired: defect == "expired");
        if (defect == "signature")
        {
            var parts = token.Split('.');
            parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
            token = string.Join('.', parts);
        }

        await DeniedAsync(() => fixture.ResolveAsync(token), StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task WildcardAndForgedMetadata_DoNotSelectForeignOrMissingOwnerAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var foreign = await fixture.SeedAccountAsync();
        var headers = new Metadata
        {
            { "x-account-id", foreign.Id.ToString() },
            { "x-user-id", foreign.Id.ToString() },
            { "x-principal-purpose", "personal:v1" },
        };
        var token = fixture.Token(account.IdentitySubject);
        var foreignError = await Assert.ThrowsAsync<RpcException>(() => fixture.ResolveAsync(token, foreign.Id, headers));
        var missingError = await Assert.ThrowsAsync<RpcException>(() => fixture.ResolveAsync(token, Guid.NewGuid(), headers));
        foreignError.Status.Should().Be(missingError.Status);
        foreignError.StatusCode.Should().Be(StatusCode.NotFound);
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(account.IdentitySubject, null), null, headers), StatusCode.PermissionDenied);
    }

    [Theory]
    [InlineData("blocked")]
    [InlineData("pending-deletion")]
    [InlineData("deleted")]
    public async Task InactiveAccount_DoesNotAcquireOwnershipAsync(string disposition)
    {
        var account = await fixture.SeedAccountAsync();
        await fixture.ChangeAsync(db =>
        {
            db.CustomerAccounts.Attach(account);
            if (disposition == "blocked")
            {
                account.Block();
            }
            else if (disposition == "pending-deletion")
            {
                account.MarkDeletionPending();
            }
            else
            {
                account.MarkAsDeleted();
            }
        });
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(account.IdentitySubject)), StatusCode.PermissionDenied);
    }

    [Fact]
    public async Task AliasCollisionAndRemovedAlias_FailClosedWithoutEmailMergeAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var other = await fixture.SeedAccountAsync();
        await fixture.ChangeAsync(db =>
        {
            db.CustomerAccounts.Attach(other);
            other.LinkIdentity(JournalHostFixture.Issuer, account.IdentitySubject, "oidc", null, DateTime.UtcNow.AddMinutes(-1));
        });
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(account.IdentitySubject)), StatusCode.NotFound);
        // Matching email and browser hints do not provision/link a missing principal.
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token("missing:" + Guid.NewGuid())), StatusCode.NotFound);
        var alias = "removed:" + Guid.NewGuid();
        await fixture.ChangeAsync(db =>
        {
            db.CustomerAccounts.Attach(other);
            other.LinkIdentity(JournalHostFixture.Issuer, alias, "oidc", null, DateTime.UtcNow.AddMinutes(-1));
        });
        await fixture.ChangeAsync(db =>
        {
            var link = other.LinkedIdentities.Single(identity => identity.ProviderSubject == alias);
            db.LinkedCustomerIdentities.Attach(link);
            link.MarkAsDeleted();
        });
        (await fixture.ReadAsync(db => db.LinkedCustomerIdentities.IgnoreQueryFilters()
            .SingleAsync(identity => identity.ProviderSubject == alias))).IsDeleted.Should().BeTrue();
        await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(alias)), StatusCode.NotFound);
        (await fixture.ResolveAsync(fixture.Token(other.IdentitySubject))).Value.Should().Be(other.Id.ToString());
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing-membership")]
    [InlineData("wrong-owner")]
    [InlineData("wrong-storefront")]
    [InlineData("blocked-membership")]
    [InlineData("revoked-membership")]
    [InlineData("deleted-membership")]
    [InlineData("deleted-profile")]
    [InlineData("quarantined-profile")]
    [InlineData("unverified-profile")]
    [InlineData("blocked-profile")]
    public async Task LegacyBridge_RequiresExactVerifiedActiveLinksOnlyAsync(string condition)
    {
        var account = await fixture.SeedAccountAsync();
        var ownerId = condition == "wrong-owner" ? (await fixture.SeedAccountAsync()).Id : account.Id;
        var storefront = Guid.NewGuid();
        var profile = CustomerProfileEntity.Create(storefront, "legacy:" + Guid.NewGuid().ToString("N"), "Fixture");
        profile.BeginAccountReconciliation(DateTime.UtcNow);
        if (condition == "quarantined-profile")
        {
            profile.QuarantineAccountReconciliation("fixture_conflict", DateTime.UtcNow);
        }
        else if (condition != "unverified-profile")
        {
            profile.CompleteAccountReconciliation(ownerId, DateTime.UtcNow);
        }

        if (condition == "blocked-profile")
        {
            profile.AccountStatus.Block("fixture", "fixture");
        }

        var membership = StorefrontMembershipEntity.Create(ownerId, storefront, DateTime.UtcNow, profile.Id);
        if (condition == "blocked-membership")
        {
            membership.Block();
        }

        if (condition == "revoked-membership")
        {
            membership.Revoke();
        }

        if (condition == "deleted-membership")
        {
            membership.RevokeAndDelete();
        }

        await fixture.ChangeAsync(db =>
        {
            db.CustomerProfiles.Add(profile);
            if (condition != "missing-membership")
            {
                db.StorefrontMemberships.Add(membership);
            }
        });
        if (condition == "deleted-profile")
        {
            await fixture.ChangeAsync(db =>
            {
                db.CustomerProfiles.Attach(profile);
                profile.MarkAsDeleted();
            });
            (await fixture.ReadAsync(db => db.CustomerProfiles.IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == profile.Id))).IsDeleted.Should().BeTrue();
        }

        var token = fixture.Token(account.IdentitySubject);
        // Invalid legacy linkage cannot disable fresh canonical ownership.
        (await fixture.ResolveAsync(token)).Value.Should().Be(account.Id.ToString());
        var requestedStorefront = condition == "wrong-storefront" ? Guid.NewGuid() : storefront;
        if (condition == "valid")
        {
            (await fixture.LegacyAsync(token, requestedStorefront, profile.Id)).Value.Should().Be(profile.Id.ToString());
        }
        else
        {
            await DeniedAsync(() => fixture.LegacyAsync(token, requestedStorefront, profile.Id), StatusCode.NotFound);
        }
    }

    [Fact]
    public async Task LegacyAccountReadRetainsItsPolicy_ProvisioningStillRequiresVerifiedEmailAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var headers = new Metadata { { "authorization", "Bearer " + fixture.Token(account.IdentitySubject, null) } };
        var client = new CustomerAccountService.CustomerAccountServiceClient(fixture.Channel);
        var read = await client.GetCustomerAccountAsync(new GetCustomerAccountRequest(), headers);
        read.Id.Value.Should().Be(account.Id.ToString());
        var denied = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.EnsureCustomerAccountAsync(new EnsureCustomerAccountRequest(), headers));
        denied.StatusCode.Should().Be(StatusCode.FailedPrecondition);
    }

    [Fact]
    public async Task LegacyCollectionRpc_PreservesSummaryDateTextAndRatingWithoutPersonalPurposeAsync()
    {
        var profile = CustomerProfileEntity.Create(Guid.NewGuid(), "legacy:" + Guid.NewGuid().ToString("N"), "Fixture");
        await fixture.ChangeAsync(db => db.CustomerProfiles.Add(profile));
        var customerId = profile.Id;
        var productId = Guid.NewGuid();
        var headers = new Metadata { { "authorization", "Bearer " + fixture.Token(customerId.ToString(), null) } };
        var client = new ProductCollectionService.ProductCollectionServiceClient(fixture.Channel);
        var first = await client.AddToCollectionAsync(new AddToCollectionRequest
        {
            CustomerId = GuidValue.FromGuid(customerId),
            ProductId = GuidValue.FromGuid(productId),
            Status = ProductCollectionStatus.Own,
            Rating = 4,
            Experience = new ProductExperienceModel { PersonalText = new string('x', 7000) },
        }, headers);
        first.Experience.PersonalText.Should().HaveLength(7000);
        first.Experience.ExperiencedAt.Should().BeNull();
        first.Rating.Should().Be(4);
        var tooLong = await Assert.ThrowsAsync<RpcException>(async () => await client.UpdateCollectionItemAsync(
            new UpdateCollectionItemRequest
            {
                CustomerId = GuidValue.FromGuid(customerId),
                ItemId = first.Id,
                Experience = new ProductExperienceModel { PersonalText = new string('x', 7001) },
            }, headers));
        tooLong.StatusCode.Should().Be(StatusCode.InvalidArgument);
        var replaced = await client.UpdateCollectionItemAsync(new UpdateCollectionItemRequest
        {
            CustomerId = GuidValue.FromGuid(customerId),
            ItemId = first.Id,
            Experience = new ProductExperienceModel { PersonalText = "replacement" },
        }, headers);
        replaced.Experience.PersonalText.Should().Be("replacement");
        replaced.Rating.Should().Be(4);
        var preserved = await client.UpdateCollectionItemAsync(new UpdateCollectionItemRequest
        {
            CustomerId = GuidValue.FromGuid(customerId),
            ItemId = first.Id,
            Notes = "preserve summary",
        }, headers);
        preserved.Experience.PersonalText.Should().Be("replacement");
        var cleared = await client.UpdateCollectionItemAsync(new UpdateCollectionItemRequest
        {
            CustomerId = GuidValue.FromGuid(customerId),
            ItemId = first.Id,
            Experience = new ProductExperienceModel(),
        }, headers);
        cleared.Experience.Should().BeNull();
        cleared.Rating.Should().Be(4);
        var read = await client.GetCollectionItemAsync(new GetCollectionItemRequest
        {
            CustomerId = GuidValue.FromGuid(customerId),
            ProductId = GuidValue.FromGuid(productId),
        }, headers);
        read.Experience.Should().BeNull();
        read.Rating.Should().Be(4);
        var recreated = await client.AddToCollectionAsync(new AddToCollectionRequest
        {
            CustomerId = GuidValue.FromGuid(customerId),
            ProductId = GuidValue.FromGuid(productId),
            Status = ProductCollectionStatus.Own,
            Experience = new ProductExperienceModel { PersonalText = "after-clear" },
        }, headers);
        recreated.Id.Should().Be(first.Id);
        recreated.Experience.PersonalText.Should().Be("after-clear");
        recreated.Rating.Should().Be(4);
        var collectionItemId = Guid.Parse(first.Id.Value);
        var summaries = await fixture.ReadAsync(db => db.ProductExperiences.IgnoreQueryFilters()
            .Where(summary => summary.CollectionItemId == collectionItemId).ToArrayAsync());
        summaries.Length.Should().BeGreaterThan(1);
        summaries.Count(summary => !summary.IsDeleted).Should().Be(1);
        summaries.Should().Contain(summary => summary.IsDeleted);
        // A downgrade must fail transactionally rather than delete retained
        // rows to recreate the former unfiltered unique index.
        var rollback = await Assert.ThrowsAsync<PostgresException>(() =>
            fixture.MigrateAsync(JournalHostFixture.PreviousMigration));
        rollback.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        (await fixture.ReadAsync(db => db.Database.GetAppliedMigrationsAsync()))
            .Should().Contain(JournalHostFixture.CurrentMigration);
        (await fixture.ReadAsync(db => db.ProductExperiences.IgnoreQueryFilters()
            .CountAsync(summary => summary.CollectionItemId == collectionItemId))).Should().Be(summaries.Length);
        (await client.GetCollectionItemAsync(new GetCollectionItemRequest
        {
            CustomerId = GuidValue.FromGuid(customerId),
            ProductId = GuidValue.FromGuid(productId),
        }, headers)).Experience.PersonalText.Should().Be("after-clear");
    }

    [Fact]
    public async Task MissingTrustedIssuerAndUnsignedPrincipalCannotSupplyJournalOwnershipAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var key = "Platform:Auth:Keycloak:Realm";
        var original = fixture.Configuration[key];
        try
        {
            fixture.Configuration[key] = null;
            await DeniedAsync(() => fixture.ResolveAsync(fixture.Token(account.IdentitySubject)), StatusCode.FailedPrecondition);
        }
        finally
        {
            fixture.Configuration[key] = original;
        }

        await DeniedAsync(() => fixture.ResolveAsync(null), StatusCode.Unauthenticated);
    }

    private static async Task DeniedAsync(Func<Task<StringValue>> action, StatusCode expected)
    {
        var exception = await Assert.ThrowsAsync<RpcException>(action);
        exception.StatusCode.Should().Be(expected);
    }
}

public sealed class JournalHostFixture : IAsyncLifetime
{
    public const string Issuer = "https://journal.fixture.invalid/realms/test";
    public const string Audience = "customer-fixture";
    public const string PreviousMigration = "20260921012532_AddPrivateStructuredProductExperience";
    public const string CurrentMigration = "20261008115437_LimitActiveProductExperienceSummary";
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.4-alpine").Build();
    private WebApplication? _app;
    public GrpcChannel Channel { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();
        // Docker-outside-of-Docker runners can reach the container network even
        // when a published host port is unavailable. Do not change Docker state.
        foreach (var endpoint in new[] { connectionString, new NpgsqlConnectionStringBuilder(connectionString)
                 { Host = _postgres.IpAddress, Port = 5432 }.ConnectionString })
        {
            try
            {
                await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(endpoint) { Timeout = 3 }.ConnectionString);
                await connection.OpenAsync();
                connectionString = endpoint;
                break;
            }
            catch (NpgsqlException) { }
        }

        var key = new RsaSecurityKey(_rsa) { KeyId = "ephemeral-journal-fixture" };
        _app = Platform.Platform.CreateWeb([])
            .ConfigurePlatformWebApplicationBuilder(builder =>
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Error);
                builder.Configuration.Sources.Clear();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Platform:Auth:Keycloak:AuthServerUrl"] = "https://journal.fixture.invalid",
                    ["Platform:Auth:Keycloak:Realm"] = "test",
                });
                builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0,
                    endpoint => endpoint.Protocols = HttpProtocols.Http2));
                builder.Services.AddApplication(builder.Configuration);
                builder.Services.AddCustomerInfrastructure(builder.Configuration);
                builder.Services.AddMediatR(options => options.RegisterServicesFromAssembly(typeof(ConfigureServices).Assembly));
                builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
                builder.Services.AddSingleton(Substitute.For<Platform.Domain.Events.IPlatformDomainEventDispatcher>());
                builder.Services.AddSingleton(Substitute.For<Platform.Outbox.IPlatformEventPublisher>());
            })
            .AddPlatformKeycloakAuth((options, _) =>
            {
                options.AuthServerUrl = "https://journal.fixture.invalid";
                options.Realm = "test";
                options.ClientId = Audience;
                options.ValidateIssuer = true;
                options.ValidateAudience = true;
                options.ClockSkew = TimeSpan.Zero;
                options.AccessTokenCookieName = null;
                options.SignalRPathPrefixes = [];
                options.IncludeErrorDetails = false;
            })
            .AddGrpcCurrentUser()
            .AddPlatformAuthorization(policies => policies.AddRolePolicy(
                CustomerServiceAuthorizationPolicies.CustomerAccess,
                PlatformRoles.Realm.SuperAdmin, PlatformRoles.Realm.Admin,
                PlatformRoles.FullAccess, PlatformRoles.Admin.CustomerManager))
            .AddGrpcStorefrontContext()
            .ConfigurePlatformWebApplicationBuilder(builder =>
            {
                builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                    options.Configuration.SigningKeys.Add(key);
                    options.TokenValidationParameters.IssuerSigningKey = key;
                    var previous = options.Events.OnTokenValidated;
                    options.Events.OnTokenValidated = async context =>
                    {
                        await previous(context);
                        ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(new Claim("fixture-previous-event", "ran"));
                    };
                });
                builder.Services.AddCustomerJournalOwnership();
            })
            .AddPlatformGrpc(grpc =>
            {
                grpc.MapService<JournalOwnerFixtureRpc>();
                grpc.MapService<CustomerAccountGrpcService>();
                grpc.MapService<ProductCollectionGrpcService>();
            })
            .Build();
        Configuration = _app.Services.GetRequiredService<IConfiguration>();
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }
        // Exercise generated Down/Up on the empty disposable database first.
        await MigrateAsync(PreviousMigration);
        await MigrateAsync(CurrentMigration);

        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Channel = GrpcChannel.ForAddress(address);
    }

    public async Task DisposeAsync()
    {
        Channel?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        _rsa.Dispose();
    }

    public async Task<CustomerAccountEntity> SeedAccountAsync()
    {
        var account = CustomerAccountEntity.Create(Issuer, "human:" + Guid.NewGuid().ToString("N"),
            "same-fixture-email@example.invalid", null, null, "en", DateTime.UtcNow.AddMinutes(-1));
        await ChangeAsync(db => db.CustomerAccounts.Add(account));
        return account;
    }

    public async Task ChangeAsync(Action<AppDbContext> action)
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        action(db);
        await db.SaveChangesAsync();
    }

    public async Task MigrateAsync(string target)
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().GetService<IMigrator>().MigrateAsync(target);
    }

    public async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public string Token(string subject, string? purpose = "\"personal:v1\"", string extra = "",
        string issuer = Issuer, string audience = Audience, bool expired = false, string? subjectJson = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = "{\"iss\":" + JsonSerializer.Serialize(issuer) + ",\"aud\":" + JsonSerializer.Serialize(audience) +
            ",\"sub\":" + (subjectJson ?? JsonSerializer.Serialize(subject)) + ",\"iat\":" + (now - 60) +
            ",\"nbf\":" + (now - 60) + ",\"exp\":" + (expired ? now - 10 : now + 300) +
            ",\"email\":\"same-fixture-email@example.invalid\",\"email_verified\":false," +
            "\"realm_access\":{\"roles\":" + JsonSerializer.Serialize(new[]
                { PlatformRoles.Realm.SuperAdmin, PlatformRoles.Realm.Customer, PlatformRoles.FullAccess }) + "}" +
            (purpose is null ? "" : ",\"dkh_principal_purpose\":" + purpose) + extra + "}";
        const string header = /*lang=json,strict*/ "{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"ephemeral-journal-fixture\"}";
        var encoded = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(header)) + "." + Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payload));
        var signature = _rsa.SignData(Encoding.UTF8.GetBytes(encoded), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return encoded + "." + Base64UrlEncoder.Encode(signature);
    }

    public async Task<StringValue> ResolveAsync(string? token, Guid? target = null, Metadata? extra = null)
    {
        var headers = new Metadata();
        if (token is not null)
        {
            headers.Add("authorization", "Bearer " + token);
        }

        if (extra is not null)
        {
            foreach (var entry in extra)
            {
                headers.Add(entry);
            }
        }

        return await Channel.CreateCallInvoker().AsyncUnaryCall(JournalOwnerFixtureRpc.ResolveMethod, null,
            new CallOptions(headers), new StringValue { Value = target?.ToString() ?? "" });
    }

    public async Task<StringValue> LegacyAsync(string token, Guid storefront, Guid profile)
        => await Channel.CreateCallInvoker().AsyncUnaryCall(JournalOwnerFixtureRpc.LegacyMethod, null,
            new CallOptions(new Metadata { { "authorization", "Bearer " + token } }),
            new StringValue { Value = storefront + ":" + profile });
}

// Test-only RPC binding invokes the production adapter over real HTTP/2,
// authorization and PostgreSQL. It does not introduce or claim EJ03 diary RPCs.
[Authorize]
[BindServiceMethod(typeof(JournalOwnerFixtureRpc), nameof(BindService))]
public class JournalOwnerFixtureRpc(CustomerJournalOwnerResolver resolver)
{
    private static readonly Marshaller<StringValue> Payload = Marshallers.Create(
        value => value.ToByteArray(), bytes => StringValue.Parser.ParseFrom(bytes));
    public static readonly Method<StringValue, StringValue> ResolveMethod = new(MethodType.Unary,
        "fixture.JournalOwner", "ResolveAsync", Payload, Payload);
    public static readonly Method<StringValue, StringValue> LegacyMethod = new(MethodType.Unary,
        "fixture.JournalOwner", "LegacyAsync", Payload, Payload);

    public static void BindService(ServiceBinderBase binder, JournalOwnerFixtureRpc? service)
    {
        binder.AddMethod(ResolveMethod, service is null ? null : service.ResolveAsync);
        binder.AddMethod(LegacyMethod, service is null ? null : service.LegacyAsync);
    }

    public virtual async Task<StringValue> ResolveAsync(StringValue request, ServerCallContext context)
    {
        context.GetHttpContext().User.HasClaim("fixture-previous-event", "ran").Should().BeTrue();
        var owner = string.IsNullOrEmpty(request.Value)
            ? await resolver.ResolveAsync(context)
            : await resolver.RequireOwnerAsync(context, Guid.Parse(request.Value));
        return new StringValue { Value = owner.AccountId.ToString() };
    }

    public virtual async Task<StringValue> LegacyAsync(StringValue request, ServerCallContext context)
    {
        var ids = request.Value.Split(':');
        var profile = await resolver.ResolveLegacyProfileAsync(context, Guid.Parse(ids[0]), Guid.Parse(ids[1]));
        return new StringValue { Value = profile.ToString() };
    }
}
