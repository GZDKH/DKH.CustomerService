using DKH.CustomerService.Domain.Entities.CustomerProfile;

namespace DKH.CustomerService.Application.Abstractions;

public interface ICustomerRepository
{
    Task<CustomerProfileEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CustomerProfileEntity?> GetByUserIdAsync(Guid storefrontId, string userId, CancellationToken cancellationToken = default);

    Task<CustomerProfileEntity?> GetByUserIdWithAddressesAsync(Guid storefrontId, string userId, CancellationToken cancellationToken = default);

    Task<CustomerProfileEntity?> GetByUserIdWithWishlistAsync(Guid storefrontId, string userId, CancellationToken cancellationToken = default);

    Task<CustomerProfileEntity> AddAsync(CustomerProfileEntity entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(CustomerProfileEntity entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(CustomerProfileEntity entity, CancellationToken cancellationToken = default);

    Task<CustomerProfileEntity?> GetByExternalIdentityAsync(
        Guid storefrontId,
        string provider,
        string providerUserId,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileEntity?> GetByUserIdWithExternalIdentitiesAsync(
        Guid storefrontId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileEntity?> FindByEmailAcrossProvidersAsync(
        Guid storefrontId,
        string email,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CustomerProfileEntity> Items, int TotalCount)> SearchAsync(
        Guid? storefrontId,  // Nullable for admin - returns all customers when null
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CustomerProfileEntity> Items, int TotalCount)> ListAsync(
        Guid? storefrontId,  // Nullable for admin - returns all customers when null
        int page,
        int pageSize,
        string? sortBy,
        bool sortDescending,
        CancellationToken cancellationToken = default);
}
