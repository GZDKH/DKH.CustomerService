using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Infrastructure.Persistence.Repositories;

public class CustomerRepository(AppDbContext dbContext) : ICustomerRepository
{
    public async Task<CustomerProfileEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<CustomerProfileEntity?> GetByUserIdAsync(
        Guid storefrontId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId && p.UserId == userId,
                cancellationToken);
    }

    public async Task<CustomerProfileEntity?> GetByUserIdWithAddressesAsync(
        Guid storefrontId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .Include(p => p.Addresses)
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId && p.UserId == userId,
                cancellationToken);
    }

    public async Task<CustomerProfileEntity?> GetByUserIdWithWishlistAsync(
        Guid storefrontId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .Include(p => p.WishlistItems)
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId && p.UserId == userId,
                cancellationToken);
    }

    public async Task<CustomerProfileEntity?> GetByExternalIdentityAsync(
        Guid storefrontId,
        string provider,
        string providerUserId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .Include(p => p.ExternalIdentities)
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId &&
                     p.ExternalIdentities.Any(e => e.Provider == provider && e.ProviderUserId == providerUserId),
                cancellationToken);
    }

    public async Task<CustomerProfileEntity?> GetByUserIdWithExternalIdentitiesAsync(
        Guid storefrontId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .Include(p => p.ExternalIdentities)
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId && p.UserId == userId,
                cancellationToken);
    }

    public async Task<CustomerProfileEntity?> FindByEmailAcrossProvidersAsync(
        Guid storefrontId,
        string email,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .Include(p => p.ExternalIdentities)
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId &&
                     (p.Email == email || p.ExternalIdentities.Any(e => e.Email == email)),
                cancellationToken);
    }

    public async Task<CustomerProfileEntity?> GetByUserIdWithAllRelationsAsync(
        Guid storefrontId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .Include(p => p.Addresses)
            .Include(p => p.WishlistItems)
            .Include(p => p.ExternalIdentities)
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId && p.UserId == userId,
                cancellationToken);
    }

    public async Task<CustomerProfileEntity> AddAsync(CustomerProfileEntity entity, CancellationToken cancellationToken = default)
    {
        dbContext.CustomerProfiles.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(CustomerProfileEntity entity, CancellationToken cancellationToken = default)
    {
        dbContext.CustomerProfiles.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CustomerProfileEntity entity, CancellationToken cancellationToken = default)
    {
        dbContext.CustomerProfiles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<CustomerProfileEntity> Items, int TotalCount)> SearchAsync(
        Guid? storefrontId,  // Nullable for admin - returns all customers when null
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = dbContext.CustomerProfiles.AsQueryable();

        // Filter by storefront only if provided (for admin, null means all storefronts)
        if (storefrontId.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.StorefrontId == storefrontId.Value);
        }

        baseQuery = baseQuery.Where(p =>
            EF.Functions.ILike(p.FirstName, $"%{query}%") ||
            (p.LastName != null && EF.Functions.ILike(p.LastName, $"%{query}%")) ||
            (p.Email != null && EF.Functions.ILike(p.Email, $"%{query}%")) ||
            (p.Phone != null && EF.Functions.Like(p.Phone, $"%{query}%")) ||
            EF.Functions.Like(p.UserId, $"%{query}%"));

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(p => p.CreationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<CustomerProfileEntity> Items, int TotalCount)> ListAsync(
        Guid? storefrontId,  // Nullable for admin - returns all customers when null
        int page,
        int pageSize,
        string? sortBy,
        bool sortDescending,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = dbContext.CustomerProfiles.AsQueryable();

        // Filter by storefront only if provided (for admin, null means all storefronts)
        if (storefrontId.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.StorefrontId == storefrontId.Value);
        }

        baseQuery = (sortBy ?? string.Empty).ToUpperInvariant() switch
        {
            "FIRSTNAME" => sortDescending
                ? baseQuery.OrderByDescending(p => p.FirstName)
                : baseQuery.OrderBy(p => p.FirstName),
            "LASTNAME" => sortDescending
                ? baseQuery.OrderByDescending(p => p.LastName)
                : baseQuery.OrderBy(p => p.LastName),
            "EMAIL" => sortDescending
                ? baseQuery.OrderByDescending(p => p.Email)
                : baseQuery.OrderBy(p => p.Email),
            _ => sortDescending
                ? baseQuery.OrderByDescending(p => p.CreationTime)
                : baseQuery.OrderBy(p => p.CreationTime),
        };

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
