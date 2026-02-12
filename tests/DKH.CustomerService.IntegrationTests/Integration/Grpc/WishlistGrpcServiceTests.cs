using DKH.Platform.Grpc.Common.Types;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform.EntityFrameworkCore.Repositories;
using DKH.Platform.Grpc.IntegrationTesting;
using DKH.Platform.IntegrationTesting;
using DKH.Platform.MultiTenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.Grpc;

[Trait("Category", "Integration")]
public class WishlistGrpcServiceTests : PlatformIntegrationTest
{
    private readonly Guid _storefrontId = Guid.NewGuid();
    private const string TelegramUserId = "tg-user-1";

    private PlatformGrpcTestFactory<GrpcTestExceptionPolicy> CreateFactory(
        Action<IServiceCollection>? configure = null)
    {
        var dbName = $"wishlist-grpc-{Guid.NewGuid()}";
        var emptyConfig = new ConfigurationBuilder().Build();

        return this.CreatePlatformGrpcTest<GrpcTestExceptionPolicy>(
                platformBuilder => platformBuilder
                    .AddPlatformRepositories<AppDbContext>(),
                typeof(CustomerProfileGrpcService),
                typeof(WishlistGrpcService),
                typeof(CustomerAddressGrpcService),
                typeof(CustomerPreferencesGrpcService))
            .WithPlatformConfiguration(services =>
            {
                services.AddSingleton(new Dictionary<Type, object>());

                services.AddMediatR(cfg =>
                    cfg.RegisterServicesFromAssembly(typeof(ConfigureServices).Assembly));
                services.AddApplication(emptyConfig);
                services.AddCustomerInfrastructure(emptyConfig);

                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));
                services.AddScoped<IAppDbContext>(sp =>
                    sp.GetRequiredService<AppDbContext>());

                services.AddSingleton(Substitute.For<IPlatformStorefrontContext>());

                configure?.Invoke(services);
            });
    }

    private async Task EnsureProfileExistsAsync(
        CustomerProfileService.CustomerProfileServiceClient profileClient)
    {
        await profileClient.GetOrCreateProfileAsync(new GetOrCreateProfileRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            FirstName = "Test",
        });
    }

    [Fact]
    public async Task AddToWishlist_AddsProductAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<WishlistService.WishlistServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var productId = Guid.NewGuid().ToString();
        var response = await client.AddToWishlistAsync(new AddToWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            ProductId = new GuidValue { Value = productId },
        });

        response.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWishlist_ReturnsAddedItemsAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<WishlistService.WishlistServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var productId = Guid.NewGuid().ToString();
        await client.AddToWishlistAsync(new AddToWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            ProductId = new GuidValue { Value = productId },
        });

        var response = await client.GetWishlistAsync(new GetWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            Pagination = new PaginationRequest { Page = 1, PageSize = 10 },
        });

        response.Wishlist.Items.Should().HaveCount(1);
        response.Wishlist.Items[0].ProductId.Value.Should().Be(productId);
    }

    [Fact]
    public async Task RemoveFromWishlist_RemovesProductAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<WishlistService.WishlistServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var productId = Guid.NewGuid().ToString();
        await client.AddToWishlistAsync(new AddToWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            ProductId = new GuidValue { Value = productId },
        });

        var response = await client.RemoveFromWishlistAsync(new RemoveFromWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            ProductId = new GuidValue { Value = productId },
        });

        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CheckProductInWishlist_WhenExists_ReturnsTrueAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<WishlistService.WishlistServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var productId = Guid.NewGuid().ToString();
        await client.AddToWishlistAsync(new AddToWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            ProductId = new GuidValue { Value = productId },
        });

        var response = await client.CheckProductInWishlistAsync(new CheckProductInWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            ProductId = new GuidValue { Value = productId },
        });

        response.InWishlist.Should().BeTrue();
    }

    [Fact]
    public async Task ClearWishlist_RemovesAllItemsAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<WishlistService.WishlistServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        await client.AddToWishlistAsync(new AddToWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            ProductId = new GuidValue { Value = Guid.NewGuid().ToString() },
        });

        await client.AddToWishlistAsync(new AddToWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
            ProductId = new GuidValue { Value = Guid.NewGuid().ToString() },
        });

        var response = await client.ClearWishlistAsync(new ClearWishlistRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            TelegramUserId = TelegramUserId,
        });

        response.ItemsRemoved.Should().Be(2);
    }
}
