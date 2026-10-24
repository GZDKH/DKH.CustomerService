using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Enums;

namespace DKH.CustomerService.Application.Addresses;

internal static class CustomerAddressOwnership
{
    internal static IQueryable<CustomerProfileEntity> ActiveProfiles(IAppDbContext db, Guid storefrontId, string userId)
        => db.CustomerProfiles.Where(profile => !profile.IsDeleted && profile.StorefrontId == storefrontId
            && profile.UserId == userId && profile.AccountStatus.Status == AccountStatusType.Active);

    internal static IQueryable<CustomerAddressEntity> OwnedAddresses(IAppDbContext db, Guid storefrontId, string userId)
        => from address in db.CustomerAddresses
           join profile in ActiveProfiles(db, storefrontId, userId) on address.CustomerId equals profile.Id
           select address;
}
