using DKH.CustomerService.Api.Grpc.Services;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Application.CustomerProfiles.DataExchange;
using DKH.CustomerService.Domain.Authorization;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform;
using DKH.Platform.Authentication.Keycloak;
using DKH.Platform.Authorization;
using DKH.Platform.Authorization.ResourceAccess;
using DKH.Platform.Authorization.ResourceAccess.DependencyInjection;
using DKH.Platform.Authorization.ResourceAccess.Grpc;
using DKH.Platform.Configuration;
using DKH.Platform.DataExchange;
using DKH.Platform.Domain.Events;
using DKH.Platform.EntityFrameworkCore.PostgreSQL;
using DKH.Platform.EntityFrameworkCore.Repositories;
using DKH.Platform.Grpc;
using DKH.Platform.Identity;
using DKH.Platform.Localization;
using DKH.Platform.Logging;
using DKH.Platform.Messaging;
using DKH.Platform.Messaging.MediatR;
using DKH.Platform.MultiTenancy;
using DKH.Platform.Telemetry;

await Platform
    .CreateWeb(args)
    .ConfigurePlatformWebApplicationBuilder(builder =>
    {
        builder.ConfigurePlatformStandardConfiguration();
        builder.Services.AddCustomerInfrastructure(builder.Configuration);
        builder.Services.AddApplication(builder.Configuration);
    })
    .AddPlatformMessaging(messaging =>
    {
        messaging.UseOutbox(outbox => outbox.ConnectionStringKey = "Default");
        messaging.EnableIntegrationEvents();
    })
    .AddPlatformMessagingWithMediatR(typeof(ConfigureServices).Assembly)
    .AddPlatformDomainEvents()
    .AddPlatformLogging()
    .AddPlatformTelemetry()
    .AddPlatformKeycloakAuth()
    .AddPlatformAuthorization(policies => policies.AddRolePolicy(
        CustomerServiceAuthorizationPolicies.CustomerAccess,
        PlatformRoles.Realm.SuperAdmin,
        PlatformRoles.Realm.Admin,
        PlatformRoles.FullAccess,
        PlatformRoles.Admin.CustomerManager))
    .ConfigurePlatformWebApplicationBuilder(builder =>
        builder.Services.AddPlatformResourceAccess<CustomerProfileEntity, CustomerAccessGrantEntity, Guid>(opts =>
        {
            opts.ResourceType = "customer";
            opts.DisplayName = "Customer";
            opts.GrantCreatorFullAccess = true;
            opts.CreatorGrantReason = "creator-default";
            opts.ScopeResourceTypes = ["storefront"];
            opts.BaselineRoleGrants = b =>
            {
                b.Grant(PlatformRoles.Admin.CustomerManager,
                        ResourceAccessConstants.WildcardResourceId,
                        ResourceAccessPermissions.FullAccess);
                b.Grant(PlatformRoles.Admin.StorefrontManager,
                        ResourceAccessConstants.WildcardResourceId,
                        ResourceAccessPermissions.FullAccess);
                b.Grant(PlatformRoles.Realm.StorefrontOwner,
                        ResourceAccessConstants.WildcardResourceId,
                        ResourceAccessPermissions.Read);
            };
        }))
    .AddPlatformLocalization()
    .AddPlatformDataExchangeFromAssemblyContaining<CustomerDataImportHandler>(options =>
    {
        options.Settings.BatchSize = 200;
        options.Settings.PublishBatchEvents = true;
    })
    .AddPlatformPostgreSql<AppDbContext>(options => options.ConnectionStringKey = "Default")
    .AddPlatformRepositories<AppDbContext>()
    .AddGrpcStorefrontContext()
    .AddGrpcCurrentUser()
    .AddPlatformGrpc(grpc =>
    {
        grpc.AddInterceptor<ResourceAccessGrpcInterceptor>();
        grpc.MapService<CustomerAddressGrpcService>();
        grpc.MapService<WishlistGrpcService>();
        grpc.MapService<CustomerPreferencesGrpcService>();
        grpc.MapService<ContactVerificationGrpcService>();
        grpc.MapService<CustomerCrudGrpcService>();
        grpc.MapService<CustomerManagementGrpcService>();
        grpc.MapService<IdentityLinkingGrpcService>();
        grpc.MapService<ProductCollectionGrpcService>();
        grpc.MapService<DataExchangeService>();
        grpc.MapService<CustomerGrantsGrpcService>();
        grpc.ConfigureDefaultRoute("CustomerService gRPC is running.");
    })
    .Build()
    .RunAsync();

internal static class CustomerServiceAuthorizationPolicies
{
    public const string CustomerAccess = "CustomerAccess";
}
