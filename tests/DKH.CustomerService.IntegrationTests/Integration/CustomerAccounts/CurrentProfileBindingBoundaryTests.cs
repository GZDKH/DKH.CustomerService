using System.Security.Claims;
using System.Security.Cryptography;
using DKH.CustomerService.Api;
using DKH.CustomerService.Api.Grpc.Services.CurrentProfileBinding.v2;
using DKH.CustomerService.Api.Journal;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Contracts.Customer.Api.CurrentProfileBinding.v2;
using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform.MultiTenancy;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.CustomerAccounts;

[Trait("Category", "Unit")]
public sealed class CurrentProfileBindingBoundaryTests
{
    private const string Issuer = "https://profile.fixture.invalid/realms/test";

    [Fact]
    public async Task VerifiedPersonalBearerReturnsOnlyActualProfileAndStorefrontAsync()
    {
        await using var f = await Fixture.CreateAsync();
        var result = await f.Service.GetMyActiveProfileBinding(new(), f.Call());
        Assert.Equal(f.ProfileId.ToString("D"), result.CustomerProfileId.Value);
        Assert.Equal(f.StorefrontId.ToString("D"), result.StorefrontId.Value);
        var parsed = ActiveProfileBindingModel.Parser.ParseFrom(result.ToByteArray());
        Assert.Equal(result, parsed);
        Assert.Equal(2, ActiveProfileBindingModel.Descriptor.Fields.InFieldNumberOrder().Count);
        Assert.Empty(GetMyActiveProfileBindingRequest.Descriptor.Fields.InFieldNumberOrder());
        Assert.False(f.Db.ChangeTracker.HasChanges());
        var authorization = Assert.Single(typeof(CurrentProfileBindingGrpcService).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, authorization.AuthenticationSchemes);
        Assert.Equal(CustomerServiceAuthorizationPolicies.CustomerSelfAccess, authorization.Policy);
    }

    [Theory]
    [InlineData("missing", StatusCode.Unauthenticated)]
    [InlineData("forged-claims", StatusCode.PermissionDenied)]
    [InlineData("service", StatusCode.PermissionDenied)]
    public async Task MissingForgedOrServicePrincipalCannotReadProfileAsync(string kind, StatusCode status)
    {
        await using var f = await Fixture.CreateAsync(kind == "service" ? "service:v1" : "personal:v1");
        if (kind == "missing")
        {
            f.Http.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        if (kind == "forged-claims")
        {
            f.Http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "opaque-human"), new Claim("iss", Issuer), new Claim("dkh_principal_purpose", "personal:v1")], "injected"))
            };
        }

        var failure = await Assert.ThrowsAsync<RpcException>(() => f.Service.GetMyActiveProfileBinding(new(), f.Call()));
        Assert.Equal(status, failure.StatusCode);
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("comma")]
    [InlineData("empty-guid")]
    [InlineData("malformed")]
    [InlineData("conflicting-context")]
    public async Task InvalidOrAmbiguousStorefrontCannotReadProfileAsync(string kind)
    {
        await using var f = await Fixture.CreateAsync();
        var headers = new Metadata();
        if (kind != "missing")
        {
            headers.Add("x-storefront-id", kind switch
            {
                "comma" => $"{f.StorefrontId:D},{f.StorefrontId:D}",
                "empty-guid" => Guid.Empty.ToString("D"),
                "malformed" => "not-a-guid",
                _ => f.StorefrontId.ToString("D")
            });
        }

        if (kind == "duplicate")
        {
            headers.Add("x-storefront-id", f.StorefrontId.ToString("D"));
        }

        if (kind == "conflicting-context")
        {
            f.Storefront.StorefrontId.Returns(Guid.NewGuid());
        }

        var failure = await Assert.ThrowsAsync<RpcException>(() => f.Service.GetMyActiveProfileBinding(new(), f.Call(headers)));
        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ForeignStorefrontRefusesWithoutOwnerDetailsAsync()
    {
        await using var f = await Fixture.CreateAsync();
        var failure = await Assert.ThrowsAsync<RpcException>(() => f.Service.GetMyActiveProfileBinding(new(),
            f.Call(new Metadata { { "x-storefront-id", Guid.NewGuid().ToString("D") } })));
        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
        Assert.Equal("ACTIVE_PROFILE_UNAVAILABLE", failure.Status.Detail);
    }

    private sealed class Fixture(TestContext db, CurrentProfileBindingGrpcService service, IPlatformStorefrontContext storefront,
        HttpContext http, Guid storefrontId, Guid profileId) : IAsyncDisposable
    {
        public TestContext Db => db;
        public CurrentProfileBindingGrpcService Service => service;
        public IPlatformStorefrontContext Storefront => storefront;
        public HttpContext Http { get; set; } = http;
        public Guid StorefrontId => storefrontId;
        public Guid ProfileId => profileId;
        public CallContext Call(Metadata? headers = null) => new(Http,
            headers ?? new Metadata { { "x-storefront-id", storefrontId.ToString("D") } });

        public static async Task<Fixture> CreateAsync(string purpose = "personal:v1")
        {
            using var rsa = RSA.Create(2048);
            var key = new RsaSecurityKey(rsa) { KeyId = "ephemeral-profile-test" };
            var handler = new JsonWebTokenHandler();
            var token = handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = "customer-fixture",
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
                Subject = new ClaimsIdentity([new Claim("sub", "opaque-human"), new Claim("dkh_principal_purpose", purpose)])
            });
            var validation = await handler.ValidateTokenAsync(token, new TokenValidationParameters
            { IssuerSigningKey = key, ValidIssuer = Issuer, ValidAudience = "customer-fixture", ClockSkew = TimeSpan.Zero });
            Assert.True(validation.IsValid);
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(validation.ClaimsIdentity) };
            var services = new ServiceCollection();
            services.AddOptions();
            services.AddCustomerJournalOwnership();
            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
            await options.Events.OnTokenValidated(new TokenValidatedContext(http,
                new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)), options)
            { Principal = http.User, SecurityToken = validation.SecurityToken });
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Platform:Auth:Keycloak:AuthServerUrl"] = "https://profile.fixture.invalid", ["Platform:Auth:Keycloak:Realm"] = "test" }).Build();
            var db = new TestContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var now = DateTime.UtcNow.AddMinutes(-1);
            var account = CustomerAccountEntity.Create(Issuer, "opaque-human", "synthetic@example.invalid", null, null, "en", now);
            var profile = CustomerProfileEntity.Create(Guid.NewGuid(), "legacy-distinct", "Synthetic");
            profile.BeginAccountReconciliation(now);
            profile.CompleteAccountReconciliation(account.Id, now);
            db.AddRange(account, profile, StorefrontMembershipEntity.Create(account.Id, profile.StorefrontId, now, profile.Id));
            await db.SaveChangesAsync();
            var accounts = new CustomerJournalAccounts(db);
            var storefront = Substitute.For<IPlatformStorefrontContext>();
            var service = new CurrentProfileBindingGrpcService(new(accounts, configuration), new(db, accounts), storefront,
                Options.Create(new PlatformStorefrontContextOptions()));
            return new(db, service, storefront, http, profile.StorefrontId, profile.Id);
        }

        public ValueTask DisposeAsync() => db.DisposeAsync();
    }

    private sealed class TestContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override Guid? GetCurrentUserId() => null;
    }

    private sealed class CallContext : ServerCallContext
    {
        private readonly Metadata _headers;
        public CallContext(HttpContext http, Metadata headers)
        {
            _headers = headers;
            UserState["__HttpContext"] = http;
        }
        protected override string MethodCore => string.Empty;
        protected override string HostCore => string.Empty;
        protected override string PeerCore => string.Empty;
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => _headers;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(string.Empty, []);
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
