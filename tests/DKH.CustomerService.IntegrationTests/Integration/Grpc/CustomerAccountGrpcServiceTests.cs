using System.Security.Claims;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform.Authorization;
using DKH.Platform.Grpc.IntegrationTesting;
using DKH.Platform.Identity;
using DKH.Platform.IntegrationTesting;
using DKH.Platform.MultiTenancy;
using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.Grpc;

[Trait("Category", "Integration")]
public sealed class CustomerAccountGrpcServiceTests : PlatformIntegrationTest
{
    private readonly Guid _storefrontId = Guid.NewGuid();

    [Fact]
    public async Task EnsureMembership_DerivesIdentityAndStorefrontWithoutAdminRoleAsync()
    {
        await using var factory = CreateFactory();
        var client = this.CreateGrpcClient<
            CustomerAccountService.CustomerAccountServiceClient,
            GrpcTestExceptionPolicy>(factory);

        var membership = await client.EnsureStorefrontMembershipAsync(new EnsureStorefrontMembershipRequest());
        var account = await client.GetCustomerAccountAsync(new GetCustomerAccountRequest());

        Guid.Parse(membership.StorefrontId.Value).Should().Be(_storefrontId);
        account.VerifiedEmail.Should().Be("customer@example.com");
        account.FirstName.Should().Be("Ada");
        account.PreferredLocale.Should().Be("en-us");
    }

    [Fact]
    public async Task EnsureMembership_AmbientContextMissing_ResolvesStorefrontFromGrpcMetadataAsync()
    {
        await using var factory = CreateFactory(resolveAmbientStorefront: false);
        var client = this.CreateGrpcClient<
            CustomerAccountService.CustomerAccountServiceClient,
            GrpcTestExceptionPolicy>(factory);
        var headers = new Metadata
        {
            { "x-storefront-id", _storefrontId.ToString() },
        };

        var membership = await client.EnsureStorefrontMembershipAsync(
            new EnsureStorefrontMembershipRequest(),
            headers);

        Guid.Parse(membership.StorefrontId.Value).Should().Be(_storefrontId);
    }

    [Fact]
    public async Task EnsureMembership_AmbientContextMissingAndMetadataRepeated_ResolvesSingleStorefrontAsync()
    {
        await using var factory = CreateFactory(resolveAmbientStorefront: false);
        var client = this.CreateGrpcClient<
            CustomerAccountService.CustomerAccountServiceClient,
            GrpcTestExceptionPolicy>(factory);
        var headers = new Metadata
        {
            { "x-storefront-id", _storefrontId.ToString() },
            { "x-storefront-id", _storefrontId.ToString() },
        };

        var membership = await client.EnsureStorefrontMembershipAsync(
            new EnsureStorefrontMembershipRequest(),
            headers);

        Guid.Parse(membership.StorefrontId.Value).Should().Be(_storefrontId);
    }

    [Fact]
    public async Task EnsureMembership_AmbientContextMissingAndMetadataCoalesced_ResolvesSingleStorefrontAsync()
    {
        await using var factory = CreateFactory(resolveAmbientStorefront: false);
        var client = this.CreateGrpcClient<
            CustomerAccountService.CustomerAccountServiceClient,
            GrpcTestExceptionPolicy>(factory);
        var headers = new Metadata
        {
            { "x-storefront-id", $"{_storefrontId},{_storefrontId}" },
        };

        var membership = await client.EnsureStorefrontMembershipAsync(
            new EnsureStorefrontMembershipRequest(),
            headers);

        Guid.Parse(membership.StorefrontId.Value).Should().Be(_storefrontId);
    }

    [Fact]
    public async Task EnsureMembership_AmbientContextMissingAndMetadataConflicts_IsRejectedAsync()
    {
        await using var factory = CreateFactory(resolveAmbientStorefront: false);
        var client = this.CreateGrpcClient<
            CustomerAccountService.CustomerAccountServiceClient,
            GrpcTestExceptionPolicy>(factory);
        var headers = new Metadata
        {
            { "x-storefront-id", _storefrontId.ToString() },
            { "x-storefront-id", Guid.NewGuid().ToString() },
        };

        var action = () => client.EnsureStorefrontMembershipAsync(
            new EnsureStorefrontMembershipRequest(),
            headers).ResponseAsync;

        await action.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.FailedPrecondition);
    }

    [Fact]
    public async Task EnsureMembership_AmbientContextMissingAndMetadataMixesValidAndInvalid_IsRejectedAsync()
    {
        await using var factory = CreateFactory(resolveAmbientStorefront: false);
        var client = this.CreateGrpcClient<
            CustomerAccountService.CustomerAccountServiceClient,
            GrpcTestExceptionPolicy>(factory);
        var headers = new Metadata
        {
            { "x-storefront-id", _storefrontId.ToString() },
            { "x-storefront-id", "not-a-guid" },
        };

        var action = () => client.EnsureStorefrontMembershipAsync(
            new EnsureStorefrontMembershipRequest(),
            headers).ResponseAsync;

        await action.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.FailedPrecondition);
    }

    [Fact]
    public async Task EnsureMembership_AmbientContextMissingAndMetadataInvalid_IsRejectedAsync()
    {
        await using var factory = CreateFactory(resolveAmbientStorefront: false);
        var client = this.CreateGrpcClient<
            CustomerAccountService.CustomerAccountServiceClient,
            GrpcTestExceptionPolicy>(factory);
        var headers = new Metadata
        {
            { "x-storefront-id", "not-a-guid" },
        };

        var action = () => client.EnsureStorefrontMembershipAsync(
            new EnsureStorefrontMembershipRequest(),
            headers).ResponseAsync;

        await action.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.FailedPrecondition);
    }

    [Fact]
    public async Task EnsureAccount_WithoutVerifiedEmailProof_IsRejectedAsync()
    {
        await using var factory = CreateFactory(emailVerified: false);
        var client = this.CreateGrpcClient<
            CustomerAccountService.CustomerAccountServiceClient,
            GrpcTestExceptionPolicy>(factory);

        var action = () => client.EnsureCustomerAccountAsync(new EnsureCustomerAccountRequest()).ResponseAsync;

        await action.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.FailedPrecondition);
    }

    private PlatformGrpcTestFactory<GrpcTestExceptionPolicy> CreateFactory(
        bool emailVerified = true,
        bool resolveAmbientStorefront = true)
    {
        const string subject = "keycloak-customer-subject";
        var databaseName = $"customer-account-grpc-{Guid.NewGuid()}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Platform:Auth:Keycloak:AuthServerUrl"] = "https://auth.xnata.com",
                ["Platform:Auth:Keycloak:Realm"] = "dkh",
            })
            .Build();
        Claim[] claims =
        [
            new("sub", subject),
            new(ClaimTypes.Email, "customer@example.com"),
            new("email_verified", emailVerified.ToString()),
            new("given_name", "Ada"),
            new("family_name", "Lovelace"),
            new("locale", "en-US"),
        ];
        var currentUser = Substitute.For<IPlatformCurrentUser>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.Claims.Returns(claims);
        currentUser.GetClaim(Arg.Any<string>()).Returns(call =>
            claims.FirstOrDefault(claim => claim.Type == call.Arg<string>())?.Value);
        var storefrontContext = Substitute.For<IPlatformStorefrontContext>();
        storefrontContext.StorefrontId.Returns(resolveAmbientStorefront ? _storefrontId : null);

        return this.CreatePlatformGrpcTest<GrpcTestExceptionPolicy>(
                platformBuilder => platformBuilder.AddPlatformAuthorization(policies =>
                    policies.AddRolePolicy("CustomerAccess", PlatformRoles.Realm.SuperAdmin)),
                typeof(CustomerAccountGrpcService))
            .WithAuthenticatedUser(
                userId: Guid.NewGuid(),
                username: "customer",
                email: "customer@example.com",
                roles: [],
                permissions: [],
                tenantId: null,
                additionalClaims: claims)
            .WithPlatformConfiguration(services =>
            {
                services.AddSingleton(new Dictionary<Type, object>());
                services.AddMediatR(configure =>
                    configure.RegisterServicesFromAssembly(typeof(ConfigureServices).Assembly));
                services.AddApplication(configuration);
                services.AddCustomerInfrastructure(configuration);

                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.AddScoped<IAppDbContext>(provider =>
                    provider.GetRequiredService<AppDbContext>());

                services.RemoveAll<IConfiguration>();
                services.AddSingleton<IConfiguration>(configuration);
                services.RemoveAll<IPlatformCurrentUser>();
                services.AddSingleton(currentUser);
                services.RemoveAll<IPlatformStorefrontContext>();
                services.AddSingleton(storefrontContext);

                services.AddSingleton(Substitute.For<Platform.Domain.Events.IPlatformDomainEventDispatcher>());
                services.AddSingleton(Substitute.For<Platform.Outbox.IPlatformEventPublisher>());
            });
    }
}
