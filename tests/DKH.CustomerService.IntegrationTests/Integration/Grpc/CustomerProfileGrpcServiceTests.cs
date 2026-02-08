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
public class CustomerProfileGrpcServiceTests : PlatformIntegrationTest
{
    private readonly Guid _storefrontId = Guid.NewGuid();
    private const string TelegramUserId = "tg-user-1";

    private PlatformGrpcTestFactory<GrpcTestExceptionPolicy> CreateFactory(
        Action<IServiceCollection>? configure = null)
    {
        var dbName = $"customer-profile-grpc-{Guid.NewGuid()}";
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

    [Fact]
    public async Task GetOrCreateProfile_NewCustomer_CreatesProfileAsync()
    {
        await using var factory = CreateFactory();
        var client = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);

        var response = await client.GetOrCreateProfileAsync(new GetOrCreateProfileRequest
        {
            StorefrontId = _storefrontId.ToString(),
            TelegramUserId = TelegramUserId,
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            LanguageCode = "en",
        });

        response.Created.Should().BeTrue();
        response.Profile.Should().NotBeNull();
        response.Profile.FirstName.Should().Be("John");
        response.Profile.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetOrCreateProfile_ExistingCustomer_ReturnsExistingAsync()
    {
        await using var factory = CreateFactory();
        var client = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);

        await client.GetOrCreateProfileAsync(new GetOrCreateProfileRequest
        {
            StorefrontId = _storefrontId.ToString(),
            TelegramUserId = TelegramUserId,
            FirstName = "John",
            LastName = "Doe",
        });

        var response = await client.GetOrCreateProfileAsync(new GetOrCreateProfileRequest
        {
            StorefrontId = _storefrontId.ToString(),
            TelegramUserId = TelegramUserId,
            FirstName = "Jane",
        });

        response.Created.Should().BeFalse();
        response.Profile.FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task GetProfile_WhenExists_ReturnsProfileAsync()
    {
        await using var factory = CreateFactory();
        var client = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);

        await client.GetOrCreateProfileAsync(new GetOrCreateProfileRequest
        {
            StorefrontId = _storefrontId.ToString(),
            TelegramUserId = TelegramUserId,
            FirstName = "John",
        });

        var response = await client.GetProfileAsync(new GetProfileRequest
        {
            StorefrontId = _storefrontId.ToString(),
            TelegramUserId = TelegramUserId,
        });

        response.Profile.Should().NotBeNull();
        response.Profile.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task GetProfile_WhenNotExists_ReturnsEmptyAsync()
    {
        await using var factory = CreateFactory();
        var client = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);

        var response = await client.GetProfileAsync(new GetProfileRequest
        {
            StorefrontId = _storefrontId.ToString(),
            TelegramUserId = "nonexistent-user",
        });

        response.Profile.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfile_SoftDelete_MarksAsDeletedAsync()
    {
        await using var factory = CreateFactory();
        var client = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);

        await client.GetOrCreateProfileAsync(new GetOrCreateProfileRequest
        {
            StorefrontId = _storefrontId.ToString(),
            TelegramUserId = TelegramUserId,
            FirstName = "John",
        });

        var response = await client.DeleteProfileAsync(new DeleteProfileRequest
        {
            StorefrontId = _storefrontId.ToString(),
            TelegramUserId = TelegramUserId,
            HardDelete = false,
        });

        response.Should().NotBeNull();
    }
}
