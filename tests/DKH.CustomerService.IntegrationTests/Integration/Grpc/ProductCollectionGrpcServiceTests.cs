using DKH.CustomerService.Api;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Customer.Api.ProductCollection.v1;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform.Authorization;
using DKH.Platform.EntityFrameworkCore.Repositories;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Grpc.IntegrationTesting;
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
public sealed class ProductCollectionGrpcServiceTests : PlatformIntegrationTest
{
    [Fact]
    public async Task CollectionData_IsOwnerScopedAcrossReadUpdateAndDeleteAttemptsAsync()
    {
        var databaseName = $"product-collection-grpc-{Guid.NewGuid()}";
        var ownerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await using var ownerFactory = CreateFactory(databaseName, ownerId);
        var ownerClient = this.CreateGrpcClient<ProductCollectionService.ProductCollectionServiceClient, GrpcTestExceptionPolicy>(ownerFactory);

        var created = await ownerClient.AddToCollectionAsync(new AddToCollectionRequest
        {
            CustomerId = GuidValue.FromGuid(ownerId),
            ProductId = GuidValue.FromGuid(productId),
            Status = ProductCollectionStatus.Own,
            Experience = new ProductExperienceModel
            {
                PersonalText = "Owner-only tasting note",
                Recommendation = "Keep private",
                Tags = { "floral" },
                Observations =
                {
                    new ProductExperienceObservationModel
                    {
                        DefinitionId = GuidValue.FromGuid(Guid.NewGuid()),
                        Role = ProductExperienceObservationRole.Observation,
                        TextValue = "jasmine",
                    },
                },
            },
        });

        var ownerCollection = await ownerClient.GetCollectionAsync(new GetCollectionRequest
        {
            CustomerId = GuidValue.FromGuid(ownerId),
            Pagination = new PaginationRequest { Page = 1, PageSize = 10 },
        });
        ownerCollection.Items.Should().ContainSingle();
        ownerCollection.Items[0].Experience.PersonalText.Should().Be("Owner-only tasting note");
        ownerCollection.Items[0].Experience.Recommendation.Should().Be("Keep private");

        await using var otherFactory = CreateFactory(databaseName, otherCustomerId);
        var otherClient = this.CreateGrpcClient<ProductCollectionService.ProductCollectionServiceClient, GrpcTestExceptionPolicy>(otherFactory);

        var otherCollection = await otherClient.GetCollectionAsync(new GetCollectionRequest
        {
            CustomerId = GuidValue.FromGuid(otherCustomerId),
            Pagination = new PaginationRequest { Page = 1, PageSize = 10 },
        });
        otherCollection.Items.Should().BeEmpty();

        await AssertStatusAsync(async () => await otherClient.GetCollectionItemAsync(new GetCollectionItemRequest
        {
            CustomerId = GuidValue.FromGuid(otherCustomerId),
            ProductId = GuidValue.FromGuid(productId),
        }), StatusCode.NotFound);

        await AssertStatusAsync(async () => await otherClient.UpdateCollectionItemAsync(new UpdateCollectionItemRequest
        {
            ItemId = created.Id,
            CustomerId = GuidValue.FromGuid(otherCustomerId),
            Notes = "attacker overwrite",
        }), StatusCode.PermissionDenied);

        await AssertStatusAsync(async () => await otherClient.RemoveFromCollectionAsync(new RemoveFromCollectionRequest
        {
            ItemId = created.Id,
            CustomerId = GuidValue.FromGuid(otherCustomerId),
        }), StatusCode.PermissionDenied);

        var ownerAfterAttempts = await ownerClient.GetCollectionItemAsync(new GetCollectionItemRequest
        {
            CustomerId = GuidValue.FromGuid(ownerId),
            ProductId = GuidValue.FromGuid(productId),
        });
        ownerAfterAttempts.Experience.PersonalText.Should().Be("Owner-only tasting note");
        ownerAfterAttempts.Notes.Should().BeNull();
    }

    private PlatformGrpcTestFactory<GrpcTestExceptionPolicy> CreateFactory(string databaseName, Guid userId)
    {
        var emptyConfig = new ConfigurationBuilder().Build();

        return this.CreatePlatformGrpcTest<GrpcTestExceptionPolicy>(
                platformBuilder => platformBuilder
                    .AddPlatformRepositories<AppDbContext>()
                    .AddPlatformAuthorization(policies => policies.AddRolePolicy(
                        CustomerServiceAuthorizationPolicies.CustomerAccess,
                        PlatformRoles.Realm.SuperAdmin)),
                typeof(ProductCollectionGrpcService))
            .WithAuthenticatedUser(
                userId: userId,
                username: $"customer-{userId:N}",
                email: $"{userId:N}@dkh.local",
                roles: [PlatformRoles.Realm.SuperAdmin],
                permissions: [],
                tenantId: null,
                additionalClaims: [])
            .WithPlatformConfiguration(services =>
            {
                services.AddSingleton(new Dictionary<Type, object>());
                services.AddMediatR(cfg =>
                    cfg.RegisterServicesFromAssembly(typeof(ConfigureServices).Assembly));
                services.AddApplication(emptyConfig);
                services.AddCustomerInfrastructure(emptyConfig);

                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.AddScoped<IAppDbContext>(sp =>
                    sp.GetRequiredService<AppDbContext>());

                services.AddSingleton(Substitute.For<IPlatformStorefrontContext>());
                services.AddSingleton(Substitute.For<Platform.Identity.IPlatformCurrentUser>());
                services.AddSingleton(Substitute.For<Platform.Domain.Events.IPlatformDomainEventDispatcher>());
                services.AddSingleton(Substitute.For<Platform.Outbox.IPlatformEventPublisher>());
            });
    }

    private static async Task AssertStatusAsync(Func<Task> action, StatusCode expectedStatus)
    {
        var exception = await Assert.ThrowsAsync<RpcException>(action);
        exception.StatusCode.Should().Be(expectedStatus);
    }
}
