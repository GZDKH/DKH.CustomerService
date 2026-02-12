using DKH.CustomerService.Api.Grpc.Services;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Application.CustomerProfiles.DataExchange;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform;
using DKH.Platform.Configuration;
using DKH.Platform.DataExchange;
using DKH.Platform.EntityFrameworkCore.PostgreSQL;
using DKH.Platform.EntityFrameworkCore.Repositories;
using DKH.Platform.Grpc;
using DKH.Platform.Identity;
using DKH.Platform.Localization;
using DKH.Platform.Logging;
using DKH.Platform.Messaging.MediatR;
using DKH.Platform.MultiTenancy;

await Platform
    .CreateWeb(args)
    .ConfigurePlatformWebApplicationBuilder(builder =>
    {
        builder.ConfigurePlatformStandardConfiguration();
        builder.Services.AddCustomerInfrastructure(builder.Configuration);
        builder.Services.AddApplication(builder.Configuration);
    })
    .AddPlatformMessagingWithMediatR(typeof(ConfigureServices).Assembly)
    .AddPlatformLogging()
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
        grpc.ConfigureServer(options => options.EnableDetailedErrors = true);
        grpc.MapService<CustomerProfileGrpcService>();
        grpc.MapService<CustomerAddressGrpcService>();
        grpc.MapService<WishlistGrpcService>();
        grpc.MapService<CustomerPreferencesGrpcService>();
        grpc.MapService<CustomerAdminGrpcService>();
        grpc.MapService<ContactVerificationGrpcService>();
        grpc.MapService<CustomerCrudGrpcService>();
        grpc.MapService<CustomerManagementGrpcService>();
        grpc.MapService<DataExchangeService>();
        grpc.ConfigureDefaultRoute("CustomerService gRPC is running.");
    })
    .Build()
    .RunAsync();
