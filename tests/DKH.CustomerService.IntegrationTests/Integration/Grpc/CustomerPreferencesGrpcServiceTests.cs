using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;
using DKH.CustomerService.Contracts.Customer.Api.CustomerPreferencesManagement.v1;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform.EntityFrameworkCore.Repositories;
using DKH.Platform.Grpc.Common.Types;
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
public class CustomerPreferencesGrpcServiceTests : PlatformIntegrationTest
{
    private readonly Guid _storefrontId = Guid.NewGuid();
    private const string UserId = "tg-user-1";

    private PlatformGrpcTestFactory<GrpcTestExceptionPolicy> CreateFactory(
        Action<IServiceCollection>? configure = null)
    {
        var dbName = $"preferences-grpc-{Guid.NewGuid()}";
        var emptyConfig = new ConfigurationBuilder().Build();

        return this.CreatePlatformGrpcTest<GrpcTestExceptionPolicy>(
                platformBuilder => platformBuilder
                    .AddPlatformRepositories<AppDbContext>(),
                typeof(CustomerManagementGrpcService),
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
        CustomerManagementService.CustomerManagementServiceClient profileClient)
    {
        await profileClient.GetOrCreateProfileAsync(new GetOrCreateProfileRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            FirstName = "Test",
        });
    }

    [Fact]
    public async Task GetPreferences_ReturnsDefaultPreferencesAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerManagementService.CustomerManagementServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerPreferencesManagementService.CustomerPreferencesManagementServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var response = await client.GetPreferencesAsync(new GetPreferencesRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
        });

        response.Preferences.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdatePreferences_UpdatesLanguageAndCurrencyAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerManagementService.CustomerManagementServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerPreferencesManagementService.CustomerPreferencesManagementServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var response = await client.UpdatePreferencesAsync(new UpdatePreferencesRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            PreferredLanguage = "ru",
            PreferredCurrency = "RUB",
        });

        response.Preferences.Should().NotBeNull();
        response.Preferences.PreferredLanguage.Should().Be("ru");
        response.Preferences.PreferredCurrency.Should().Be("RUB");
    }

    [Fact]
    public async Task UpdateNotificationChannels_UpdatesChannelsAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerManagementService.CustomerManagementServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerPreferencesManagementService.CustomerPreferencesManagementServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var response = await client.UpdateNotificationChannelsAsync(new UpdateNotificationChannelsRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            EmailNotificationsEnabled = true,
            TelegramNotificationsEnabled = true,
            SmsNotificationsEnabled = false,
        });

        response.Preferences.Should().NotBeNull();
        response.Preferences.EmailNotificationsEnabled.Should().BeTrue();
        response.Preferences.TelegramNotificationsEnabled.Should().BeTrue();
        response.Preferences.SmsNotificationsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateNotificationTypes_UpdatesTypesAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerManagementService.CustomerManagementServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerPreferencesManagementService.CustomerPreferencesManagementServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var response = await client.UpdateNotificationTypesAsync(new UpdateNotificationTypesRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            OrderStatusUpdates = true,
            PromotionalOffers = false,
        });

        response.Preferences.Should().NotBeNull();
        response.Preferences.OrderStatusUpdates.Should().BeTrue();
        response.Preferences.PromotionalOffers.Should().BeFalse();
    }
}
