using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.ExternalIdentity;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Domain.Entities.WishlistItem;

namespace DKH.CustomerService.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<CustomerAccountEntity> CustomerAccounts { get; }

    DbSet<LinkedCustomerIdentityEntity> LinkedCustomerIdentities { get; }

    DbSet<StorefrontMembershipEntity> StorefrontMemberships { get; }

    DbSet<CustomerProfileEntity> CustomerProfiles { get; }

    DbSet<CustomerAddressEntity> CustomerAddresses { get; }

    DbSet<CustomerExternalIdentityEntity> ExternalIdentities { get; }

    DbSet<WishlistItemEntity> WishlistItems { get; }

    DbSet<ProductCollectionItemEntity> ProductCollectionItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
