using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.CustomerService.Infrastructure.Persistence.Repositories;
using DKH.CustomerService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DKH.CustomerService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        // Expose AppDbContext as DbContext so OLAC's ApplyResourceAccessFilter and the
        // BaselineRoleGrantsSeeder can resolve a DbContext via DI.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IVerificationService, NullVerificationService>();
        return services;
    }
}
