using System.Reflection;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.ExternalIdentity;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.Platform.EntityFrameworkCore;
using DKH.Platform.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DKH.CustomerService.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : PlatformDbContext<AppDbContext>(options),
        IAppDbContext
{
    public DbSet<CustomerProfileEntity> CustomerProfiles { get; init; } = null!;

    public DbSet<CustomerAddressEntity> CustomerAddresses { get; init; } = null!;

    public DbSet<WishlistItemEntity> WishlistItems { get; init; } = null!;

    public DbSet<CustomerExternalIdentityEntity> ExternalIdentities { get; init; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    protected override Guid? GetCurrentUserId()
    {
        var currentUser = this.GetService<IPlatformCurrentUser>();
        return currentUser?.UserId;
    }
}
