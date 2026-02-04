using System.Reflection;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.Platform.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : PlatformDbContext<AppDbContext>(options),
        IAppDbContext
{
    public DbSet<CustomerProfileEntity> CustomerProfiles { get; init; } = null!;

    public DbSet<CustomerAddressEntity> CustomerAddresses { get; init; } = null!;

    public DbSet<WishlistItemEntity> WishlistItems { get; init; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
