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
public class CustomerAddressGrpcServiceTests : PlatformIntegrationTest
{
    private readonly Guid _storefrontId = Guid.NewGuid();
    private const string UserId = "tg-user-1";

    private PlatformGrpcTestFactory<GrpcTestExceptionPolicy> CreateFactory(
        Action<IServiceCollection>? configure = null)
    {
        var dbName = $"address-grpc-{Guid.NewGuid()}";
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
            UserId = UserId,
            FirstName = "Test",
        });
    }

    [Fact]
    public async Task CreateAddress_CreatesNewAddressAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerAddressService.CustomerAddressServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var response = await client.CreateAddressAsync(new CreateAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            Label = "Home",
            Country = "US",
            City = "New York",
            Street = "5th Avenue",
            Building = "123",
            PostalCode = "10001",
        });

        response.Address.Should().NotBeNull();
        response.Address.Label.Should().Be("Home");
        response.Address.City.Should().Be("New York");
    }

    [Fact]
    public async Task ListAddresses_ReturnsCreatedAddressesAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerAddressService.CustomerAddressServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        await client.CreateAddressAsync(new CreateAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            Label = "Home",
            Country = "US",
            City = "New York",
            Street = "5th Avenue",
            Building = "123",
        });

        var response = await client.ListAddressesAsync(new ListAddressesRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
        });

        response.Addresses.Should().HaveCount(1);
        response.Addresses[0].Label.Should().Be("Home");
    }

    [Fact]
    public async Task GetAddress_WhenExists_ReturnsAddressAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerAddressService.CustomerAddressServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var createResponse = await client.CreateAddressAsync(new CreateAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            Label = "Office",
            Country = "US",
            City = "Boston",
            Street = "Main St",
            Building = "1",
        });

        var response = await client.GetAddressAsync(new GetAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            AddressId = createResponse.Address.Id,
        });

        response.Found.Should().BeTrue();
        response.Address.Label.Should().Be("Office");
    }

    [Fact]
    public async Task SetDefaultAddress_SetsAddressAsDefaultAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerAddressService.CustomerAddressServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var createResponse = await client.CreateAddressAsync(new CreateAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            Label = "Home",
            Country = "US",
            City = "Chicago",
            Street = "Lake Shore Dr",
            Building = "42",
        });

        var setResponse = await client.SetDefaultAddressAsync(new SetDefaultAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            AddressId = createResponse.Address.Id,
        });

        setResponse.Success.Should().BeTrue();

        var defaultResponse = await client.GetDefaultAddressAsync(new GetDefaultAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
        });

        defaultResponse.Found.Should().BeTrue();
        defaultResponse.Address.Id.Should().Be(createResponse.Address.Id);
    }

    [Fact]
    public async Task DeleteAddress_ReturnsSuccessAsync()
    {
        await using var factory = CreateFactory();
        var profileClient = this.CreateGrpcClient<CustomerProfileService.CustomerProfileServiceClient, GrpcTestExceptionPolicy>(factory);
        var client = this.CreateGrpcClient<CustomerAddressService.CustomerAddressServiceClient, GrpcTestExceptionPolicy>(factory);

        await EnsureProfileExistsAsync(profileClient);

        var createResponse = await client.CreateAddressAsync(new CreateAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            Label = "Temp",
            Country = "US",
            City = "Miami",
            Street = "Ocean Dr",
            Building = "1",
        });

        var response = await client.DeleteAddressAsync(new DeleteAddressRequest
        {
            StorefrontId = new GuidValue { Value = _storefrontId.ToString() },
            UserId = UserId,
            AddressId = createResponse.Address.Id,
        });

        response.Success.Should().BeTrue();
    }
}
