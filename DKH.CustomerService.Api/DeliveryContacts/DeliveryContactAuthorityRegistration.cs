using DKH.CustomerService.Application.DeliveryContacts;
using DKH.DeliveryService.Contracts.Delivery.Api.GroupContactAuthority.v2;
using DKH.Platform.Grpc.Client;
using DKH.Platform.Identity.Interceptors;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DKH.CustomerService.Api.DeliveryContacts;

public static class DeliveryContactAuthorityRegistration
{
    public const string EndpointName = nameof(GroupContactAuthorityService.GroupContactAuthorityServiceClient);
    public const string EndpointUrlKey = "Platform:Grpc:Endpoints:" + EndpointName + ":Url";

    public static IServiceCollection AddCustomerDeliveryContactAuthority(this IServiceCollection services, ConfigurationManager configuration)
    {
        // An absent deployment prerequisite must not turn a missing owner into
        // an invented endpoint, or break unrelated Customer operations at startup.
        if (!string.IsNullOrWhiteSpace(configuration[EndpointUrlKey]))
        {
            services.Replace(ServiceDescriptor.Scoped<IDeliveryContactScopeAuthorizer, DeliveryContactScopeAuthorizer>());
        }

        return services;
    }

    public static void ConfigureEndpoint(IConfiguration configuration, IPlatformGrpcClientBuilder grpc)
    {
        if (string.IsNullOrWhiteSpace(configuration[EndpointUrlKey]))
        {
            return;
        }

        grpc.AddEndpointFromConfiguration<GroupContactAuthorityService.GroupContactAuthorityServiceClient>(EndpointName,
            configureOptions: options =>
            {
                options.UseServiceIdentity = false;
                options.EnableRetry = false;
                options.TimeoutSeconds = 10;
                options.AddInterceptor<PlatformUserIdentityPropagationInterceptor>();
            });
    }
}
