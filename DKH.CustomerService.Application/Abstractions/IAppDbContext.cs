using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.WishlistItem;

namespace DKH.CustomerService.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<CustomerProfileEntity> CustomerProfiles { get; }

    DbSet<CustomerAddressEntity> CustomerAddresses { get; }

    DbSet<WishlistItemEntity> WishlistItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
