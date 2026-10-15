using System.Security.Claims;
using System.Security.Cryptography;
using DKH.CustomerService.Api.DeliveryContacts;
using DKH.CustomerService.Api.Journal;
using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.DeliveryContacts;
using DKH.DeliveryService.Contracts.Delivery.Api.GroupContactAuthority.v2;
using DKH.DeliveryService.Contracts.Delivery.Models.v2;
using DKH.Platform.Grpc.Client;
using DKH.Platform.Grpc.Client.Configuration;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Identity;
using DKH.Platform.Identity.Interceptors;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

// Real published1.14 generated client + actual Platform human interceptor;
// the remote owner response below is an EXPLICIT synthetic boundary, not a
// claim that the Delivery runtime already has a valid participant producer.
[Trait("Category", "Unit")]
public sealed class DeliveryContactOwnerCallbackUnitTests
{
    [Fact]
    public async Task EveryReadUsesSameVerifiedHumanAndExactTypedOwnerTupleAsync()
    {
        var f = await Fixture.CreateAsync();
        var first = await f.ReadAsync();
        var second = await f.ReadAsync();
        f.Invoker.Calls.Should().Be(2);
        f.Invoker.Headers!.GetValue("authorization").Should().Be(f.Context.Request.Headers.Authorization.ToString());
        f.Invoker.Headers.GetAll("x-workspace-id").Should().ContainSingle().Which.Value.Should().Be(Id(f.Response.WorkspaceId).ToString("D"));
        f.Invoker.Headers.GetAll("x-storefront-id").Should().ContainSingle().Which.Value.Should().Be(f.StorefrontId.ToString("D"));
        f.Invoker.Request!.Scope.ChoiceRevisionId.Value.Should().Be(f.Scope.ChoiceRevisionId.ToString("D"));
        f.Invoker.Request.Scope.ScopeRevision.Should().Be(f.Scope.ScopeRevision);
        f.Invoker.Request.Scope.AllocationPublicRef.Should().Be("opaque allocation/NOT-a-uuid");
        first.Should().Be(second);
        first.RecipientProfileId.Should().Be(Id(f.Response.RecipientProfileBinding));
        first.RecipientConsentId.Should().Be(Id(f.Response.RecipientConsentRef));
        first.PrivateContactId.Should().Be(Id(f.Response.PrivateContactRef));
        first.WorkspaceId.Should().Be(Id(f.Response.WorkspaceId));
        first.CarrierRequiresPhone.Should().BeTrue();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("multiple")]
    [InlineData("malformed")]
    [InlineData("empty-guid")]
    public async Task InvalidWorkspaceSelectorNeverCallsOwnerAsync(string kind)
    {
        var f = await Fixture.CreateAsync();
        f.Context.Request.Headers.Remove("X-Workspace-Id");
        if (kind == "multiple")
        {
            f.Context.Request.Headers["X-Workspace-Id"] = new Microsoft.Extensions.Primitives.StringValues([Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")]);
        }
        else if (kind != "missing")
        {
            f.Context.Request.Headers["X-Workspace-Id"] = kind == "empty-guid" ? Guid.Empty.ToString("D") : "not-a-workspace";
        }

        var read = () => f.ReadAsync();
        (await read.Should().ThrowAsync<DeliveryContactFailureException>()).Which.Code.Should().Be("AUTHORITY_UNAVAILABLE");
        f.Invoker.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("group")]
    [InlineData("allocation")]
    [InlineData("plan")]
    [InlineData("scope-revision")]
    [InlineData("choice")]
    [InlineData("duty-id")]
    [InlineData("duty-revision")]
    [InlineData("duty-digest")]
    [InlineData("purpose")]
    [InlineData("storefront")]
    [InlineData("workspace")]
    [InlineData("different-workspace")]
    [InlineData("profile")]
    [InlineData("consent")]
    [InlineData("contact")]
    [InlineData("expired")]
    [InlineData("over-five-minutes")]
    [InlineData("missing-scope")]
    [InlineData("malformed-expiry")]
    public async Task IncompleteStaleOrChangedOwnerResponseNeverAuthorizesAsync(string field)
    {
        var f = await Fixture.CreateAsync();
        switch (field)
        {
            case "group":
                f.Response.Scope.GroupId = NewId();
                break;
            case "allocation":
                f.Response.Scope.AllocationPublicRef = "other";
                break;
            case "plan":
                f.Response.Scope.PlanRevisionId = NewId();
                break;
            case "scope-revision":
                f.Response.Scope.ScopeRevision++;
                break;
            case "choice":
                f.Response.Scope.ChoiceRevisionId = NewId();
                break;
            case "duty-id":
                f.Response.Scope.DutyAcceptance.ResourceId = NewId();
                break;
            case "duty-revision":
                f.Response.Scope.DutyAcceptance.Revision++;
                break;
            case "duty-digest":
                f.Response.Scope.DutyAcceptance.Digest = new string('b', 64);
                break;
            case "purpose":
                f.Response.Scope.Purpose = GroupContactPurpose.Unspecified;
                break;
            case "storefront":
                f.Response.StorefrontId = NewId();
                break;
            case "workspace":
                f.Response.WorkspaceId = null;
                break;
            case "different-workspace":
                f.Response.WorkspaceId = NewId();
                break;
            case "profile":
                f.Response.RecipientProfileBinding = null;
                break;
            case "consent":
                f.Response.RecipientConsentRef = null;
                break;
            case "contact":
                f.Response.PrivateContactRef = null;
                break;
            case "expired":
                f.Response.ExpiresAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(-1));
                break;
            case "over-five-minutes":
                f.Response.ExpiresAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(6));
                break;
            case "missing-scope":
                f.Response.Scope = null;
                break;
            case "malformed-expiry":
                f.Response.ExpiresAt = new Timestamp { Nanos = -1 };
                break;
        }

        var failure = await Assert.ThrowsAsync<DeliveryContactFailureException>(f.ReadAsync);
        failure.Code.Should().Be("AUTHORITY_UNAVAILABLE");
        f.Invoker.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(StatusCode.Unimplemented)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unauthenticated)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.FailedPrecondition)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public async Task UnavailableDeniedOrRevokedOwnerNeverBecomesPositiveAsync(StatusCode code)
    {
        var f = await Fixture.CreateAsync();
        f.Invoker.Error = new RpcException(new Status(code, "remote-private-details-not-forwarded"));
        var failure = await Assert.ThrowsAsync<DeliveryContactFailureException>(f.ReadAsync);
        failure.Code.Should().Be("AUTHORITY_UNAVAILABLE");
        failure.Message.Should().NotContain("remote-private-details");
    }

    [Fact]
    public async Task LaterOwnerRevocationIsRecheckedRatherThanCachedAsync()
    {
        var f = await Fixture.CreateAsync();
        _ = await f.ReadAsync();
        f.Invoker.Error = new RpcException(new Status(StatusCode.PermissionDenied, "revoked"));
        await Assert.ThrowsAsync<DeliveryContactFailureException>(f.ReadAsync);
        f.Invoker.Calls.Should().Be(2);
    }

    [Theory]
    [InlineData("forged-principal")]
    [InlineData("missing-bearer")]
    [InlineData("multiple-bearers")]
    public async Task CallerClaimsOrUnforwardableIdentityCannotMintOwnerAuthorityAsync(string mode)
    {
        var f = await Fixture.CreateAsync();
        if (mode == "forged-principal")
        {
            f.Accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(f.Context.User.Claims, "injected")),
            };
            f.Accessor.HttpContext.Request.Headers.Authorization = f.Context.Request.Headers.Authorization;
            (await Assert.ThrowsAsync<RpcException>(f.ReadAsync)).StatusCode.Should().Be(StatusCode.PermissionDenied);
        }
        else
        {
            f.Context.Request.Headers.Authorization = mode == "missing-bearer" ? [] : new[] { "Bearer first", "Bearer second" };
            await Assert.ThrowsAsync<DeliveryContactFailureException>(f.ReadAsync);
        }

        f.Invoker.Calls.Should().Be(0);
    }

    [Fact]
    public void MissingEndpointPreservesUnavailableAndConfiguredClientUsesHumanOnlyWithoutRetry()
    {
        var absent = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddCustomerInfrastructure(absent).AddCustomerDeliveryContactAuthority(absent);
        services.Last(x => x.ServiceType == typeof(IDeliveryContactScopeAuthorizer)).ImplementationType
            .Should().Be<UnavailableDeliveryContactScopeAuthorizer>();
        var configured = new ConfigurationManager();
        configured.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DeliveryContactAuthorityRegistration.EndpointUrlKey] = "https://owner.fixture.invalid",
        });
        services.AddCustomerDeliveryContactAuthority(configured);
        services.Last(x => x.ServiceType == typeof(IDeliveryContactScopeAuthorizer)).ImplementationType
            .Should().Be<DeliveryContactScopeAuthorizer>();
        var builder = Substitute.For<IPlatformGrpcClientBuilder>();
        PlatformGrpcClientOptions? captured = null;
        builder.When(x => x.AddEndpointFromConfiguration<GroupContactAuthorityService.GroupContactAuthorityServiceClient>(
                DeliveryContactAuthorityRegistration.EndpointName, Arg.Any<Action<PlatformGrpcClientOptions>>(), Arg.Any<Action<GrpcChannelOptions>>()))
            .Do(call =>
            {
                captured = new PlatformGrpcClientOptions(new ServiceCollection()) { UseServiceIdentity = true, EnableRetry = true };
                call.ArgAt<Action<PlatformGrpcClientOptions>>(1)(captured);
            });
        DeliveryContactAuthorityRegistration.ConfigureEndpoint(absent, builder);
        captured.Should().BeNull();
        DeliveryContactAuthorityRegistration.ConfigureEndpoint(configured, builder);
        captured!.UseServiceIdentity.Should().BeFalse();
        captured.EnableRetry.Should().BeFalse();
        captured.TimeoutSeconds.Should().Be(10);
        captured.InterceptorTypes.Should().Contain(typeof(PlatformUserIdentityPropagationInterceptor));
    }

    private static GuidValue NewId() => new() { Value = Guid.NewGuid().ToString("D") };
    private static Guid Id(GuidValue value) => Guid.Parse(value.Value);

    private sealed class Fixture
    {
        public required DefaultHttpContext Context { get; init; }
        public required TestContextAccessor Accessor { get; init; }
        public required DeliveryContactScope Scope { get; init; }
        public required RecipientContactScopeAuthorizationModel Response { get; init; }
        public required OwnerInvoker Invoker { get; init; }
        public Guid ActorAccountId { get; } = Guid.NewGuid();
        public required Guid StorefrontId { get; init; }
        public required DeliveryContactScopeAuthorizer Authorizer { get; init; }
        public Task<DeliveryContactScopeDecision> ReadAsync() => Authorizer.RevalidateAsync(ActorAccountId, StorefrontId, Scope, default);

        public static async Task<Fixture> CreateAsync()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Platform:Auth:Keycloak:AuthServerUrl"] = "https://identity.fixture.invalid",
                ["Platform:Auth:Keycloak:Realm"] = "test",
            }).Build();
            var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
            var handler = new JsonWebTokenHandler { MapInboundClaims = false };
            var token = handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = "https://identity.fixture.invalid/realms/test",
                Audience = "customer-fixture",
                Claims = new Dictionary<string, object> { ["sub"] = "human:not-an-account-uuid", ["dkh_principal_purpose"] = "personal:v1" },
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            });
            var validation = await handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                IssuerSigningKey = key,
                ValidIssuer = "https://identity.fixture.invalid/realms/test",
                ValidAudience = "customer-fixture",
            });
            validation.IsValid.Should().BeTrue();
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(validation.ClaimsIdentity) };
            context.Request.Headers.Authorization = "Bearer " + token;
            var services = new ServiceCollection();
            services.AddOptions();
            services.AddCustomerJournalOwnership();
            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
            await options.Events.OnTokenValidated(new TokenValidatedContext(context,
                new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)), options)
            { Principal = context.User, SecurityToken = validation.SecurityToken });
            var scope = new DeliveryContactScope(Guid.NewGuid(), "opaque allocation/NOT-a-uuid", Guid.NewGuid(), 7, Guid.NewGuid(),
                new DeliveryContactAuthority("delivery.duty.v2", Guid.NewGuid(), 3, new string('a', 64)));
            var response = new RecipientContactScopeAuthorizationModel
            {
                Scope = new GroupContactScopeModel
                {
                    GroupId = new() { Value = scope.GroupId.ToString("D") },
                    AllocationPublicRef = scope.AllocationPublicRef,
                    PlanRevisionId = new() { Value = scope.PlanRevisionId.ToString("D") },
                    ScopeRevision = scope.ScopeRevision,
                    ChoiceRevisionId = new() { Value = scope.ChoiceRevisionId.ToString("D") },
                    Purpose = GroupContactPurpose.LocalShipment,
                    DutyAcceptance = new AuthorityRefModel { Authority = scope.Duty.Authority, ResourceId = new() { Value = scope.Duty.ResourceId.ToString("D") }, Revision = scope.Duty.Revision, Digest = scope.Duty.Digest },
                },
                StorefrontId = NewId(),
                WorkspaceId = NewId(),
                RecipientProfileBinding = NewId(),
                RecipientConsentRef = NewId(),
                PrivateContactRef = NewId(),
                ExpiresAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(2)),
                CarrierRequiresPhone = true,
            };
            context.Request.Headers["X-Workspace-Id"] = Id(response.WorkspaceId).ToString("D");
            var invoker = new OwnerInvoker { Response = response };
            var accessor = new TestContextAccessor { HttpContext = context };
            var interceptor = new PlatformUserIdentityPropagationInterceptor(Substitute.For<IPlatformCurrentUser>(),
                Options.Create(new PlatformCurrentUserOptions { ForwardAuthorizationHeader = true }), accessor);
            var client = new GroupContactAuthorityService.GroupContactAuthorityServiceClient(invoker.Intercept(interceptor));
            return new Fixture
            {
                Context = context,
                Accessor = accessor,
                Scope = scope,
                Response = response,
                Invoker = invoker,
                StorefrontId = Id(response.StorefrontId),
                Authorizer = new DeliveryContactScopeAuthorizer(client, accessor, configuration, TimeProvider.System)
            };
        }
    }

    private sealed class TestContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class OwnerInvoker : CallInvoker
    {
        public required RecipientContactScopeAuthorizationModel Response { get; init; }
        public RpcException? Error { get; set; }
        public int Calls { get; private set; }
        public Metadata? Headers { get; private set; }
        public RevalidateRecipientContactScopeRequest? Request { get; private set; }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            Calls++;
            Headers = options.Headers;
            Request = (RevalidateRecipientContactScopeRequest)(object)request;
            var result = Error is null ? Task.FromResult((TResponse)(object)Response.Clone()) : Task.FromException<TResponse>(Error);
            return new(result, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
    }
}
