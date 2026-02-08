using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.CustomerService.Infrastructure.Persistence.Repositories;
using DKH.CustomerService.Infrastructure.Services;
using DKH.Platform.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DKH.CustomerService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IVerificationService, NullVerificationService>();
        services.AddSingleton<IPlatformDomainEventDispatcher, NullDomainEventDispatcher>();
        return services;
    }
}
