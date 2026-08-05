using DKH.CustomerService.Api;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccountAdmin.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform.Authorization;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Grpc.IntegrationTesting;
using DKH.Platform.Identity;
using DKH.Platform.IntegrationTesting;
using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.Grpc;

[Trait("Category", "Integration")]
public sealed class CustomerAccountAdminGrpcServiceTests : PlatformIntegrationTest
{
    [Fact]
    public async Task ListAccounts_WithPlatformAdminRole_IsAllowedAsync()
    {
        await using var factory = CreateFactory(PlatformRoles.Realm.SuperAdmin);
        var client = this.CreateGrpcClient<
            CustomerAccountAdminService.CustomerAccountAdminServiceClient,
            GrpcTestExceptionPolicy>(factory);

        var response = await client.ListCustomerAccountsAsync(new ListCustomerAccountsRequest());

        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ListAccounts_WithCustomerManagerRole_IsForbiddenAsync()
    {
        await using var factory = CreateFactory(PlatformRoles.Admin.CustomerManager);
        var client = this.CreateGrpcClient<
            CustomerAccountAdminService.CustomerAccountAdminServiceClient,
            GrpcTestExceptionPolicy>(factory);

        var action = () => client.ListCustomerAccountsAsync(new ListCustomerAccountsRequest()).ResponseAsync;

        await action.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.PermissionDenied);
    }

    [Fact]
    public async Task AccountMutation_WithFreeFormAuditReason_IsRejectedAsync()
    {
        await using var factory = CreateFactory(PlatformRoles.Realm.SuperAdmin);
        var client = this.CreateGrpcClient<
            CustomerAccountAdminService.CustomerAccountAdminServiceClient,
            GrpcTestExceptionPolicy>(factory);
        var request = new SetCustomerAccountStatusRequest
        {
            AccountId = GuidValue.FromGuid(Guid.NewGuid()),
            Status = CustomerAccountStatus.Blocked,
            Reason = "Customer asked by email",
        };

        var action = () => client.SetCustomerAccountStatusAsync(request).ResponseAsync;

        await action.Should().ThrowAsync<RpcException>()
            .Where(exception => exception.StatusCode == StatusCode.InvalidArgument);
    }

    private PlatformGrpcTestFactory<GrpcTestExceptionPolicy> CreateFactory(string role)
    {
        var databaseName = $"customer-account-admin-grpc-{Guid.NewGuid()}";
        var configuration = new ConfigurationBuilder().Build();
        var currentUser = Substitute.For<IPlatformCurrentUser>();
        currentUser.IsAuthenticated.Returns(true);

        return this.CreatePlatformGrpcTest<GrpcTestExceptionPolicy>(
                platformBuilder => platformBuilder.AddPlatformAuthorization(policies =>
                    policies.AddRolePolicy(
                        CustomerServiceAuthorizationPolicies.PlatformCustomerAccountAdmin,
                        PlatformRoles.Realm.SuperAdmin,
                        PlatformRoles.Realm.Admin,
                        PlatformRoles.FullAccess)),
                typeof(CustomerAccountAdminGrpcService))
            .WithAuthenticatedUser(
                userId: Guid.NewGuid(),
                username: "admin",
                email: "admin@example.com",
                roles: [role],
                permissions: [],
                tenantId: null,
                additionalClaims: [new("sub", "platform-admin-subject")])
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
                services.AddSingleton(Substitute.For<Platform.Domain.Events.IPlatformDomainEventDispatcher>());
                services.AddSingleton(Substitute.For<Platform.Outbox.IPlatformEventPublisher>());
            });
    }
}
