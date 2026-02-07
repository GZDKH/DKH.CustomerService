using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.Platform;
using DKH.Platform.Configuration;
using DKH.Platform.EntityFrameworkCore.PostgreSQL;
using DKH.Platform.EntityFrameworkCore.Repositories;
using DKH.Platform.Grpc;
using DKH.Platform.Logging;
using DKH.Platform.Messaging;
using DKH.Platform.MultiTenancy;

await Platform
    .CreateWeb(args)
    .ConfigurePlatformWebApplicationBuilder(builder =>
    {
        builder.ConfigurePlatformStandardConfiguration();
        builder.Services.AddCustomerInfrastructure(builder.Configuration);
        builder.Services.AddApplication(builder.Configuration);
    })
    .AddPlatformMessagingWithMediatR(typeof(DKH.CustomerService.Application.ConfigureServices).Assembly)
    .AddPlatformLogging()
    .AddPlatformPostgreSql<AppDbContext>(options => options.ConnectionStringKey = "Default")
    .AddPlatformRepositories<AppDbContext>()
    .AddGrpcStorefrontContext()
    .AddPlatformGrpc(grpc =>
    {
        grpc.ConfigureServer(options => options.EnableDetailedErrors = true);
        grpc.MapService<CustomerProfileGrpcService>();
        grpc.MapService<CustomerAddressGrpcService>();
        grpc.MapService<WishlistGrpcService>();
        grpc.MapService<CustomerPreferencesGrpcService>();
        grpc.MapService<CustomerAdminGrpcService>();
        grpc.MapService<ContactVerificationGrpcService>();
        grpc.ConfigureDefaultRoute("CustomerService gRPC is running.");
    })
    .Build()
    .RunAsync();
