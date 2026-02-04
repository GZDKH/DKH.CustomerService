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

    public async Task<CustomerProfileEntity?> GetByTelegramUserIdAsync(
        Guid storefrontId,
        string telegramUserId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId && p.TelegramUserId == telegramUserId,
                cancellationToken);
    }

    public async Task<CustomerProfileEntity?> GetByTelegramUserIdWithAddressesAsync(
        Guid storefrontId,
        string telegramUserId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .Include(p => p.Addresses)
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId && p.TelegramUserId == telegramUserId,
                cancellationToken);
    }

    public async Task<CustomerProfileEntity?> GetByTelegramUserIdWithWishlistAsync(
        Guid storefrontId,
        string telegramUserId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerProfiles
            .Include(p => p.WishlistItems)
            .FirstOrDefaultAsync(
                p => p.StorefrontId == storefrontId && p.TelegramUserId == telegramUserId,
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
        Guid storefrontId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = dbContext.CustomerProfiles
            .Where(p => p.StorefrontId == storefrontId)
            .Where(p =>
                EF.Functions.ILike(p.FirstName, $"%{query}%") ||
                (p.LastName != null && EF.Functions.ILike(p.LastName, $"%{query}%")) ||
                (p.Email != null && EF.Functions.ILike(p.Email, $"%{query}%")) ||
                (p.Phone != null && EF.Functions.Like(p.Phone, $"%{query}%")) ||
                EF.Functions.Like(p.TelegramUserId, $"%{query}%"));

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(p => p.CreationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<CustomerProfileEntity> Items, int TotalCount)> ListAsync(
        Guid storefrontId,
        int page,
        int pageSize,
        string? sortBy,
        bool sortDescending,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = dbContext.CustomerProfiles
            .Where(p => p.StorefrontId == storefrontId);

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
