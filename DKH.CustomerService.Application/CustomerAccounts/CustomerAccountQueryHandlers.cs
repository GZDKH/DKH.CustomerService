using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;

namespace DKH.CustomerService.Application.CustomerAccounts;

public sealed class GetCustomerAccountQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<GetCustomerAccountQuery, CustomerAccountModel>
{
    public async Task<CustomerAccountModel> Handle(
        GetCustomerAccountQuery request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountHandlerSupport.RequireAccountAsync(
            dbContext,
            request.Identity,
            includeLinkedIdentities: false,
            cancellationToken);
        return account.ToContractModel();
    }
}

public sealed class ListStorefrontMembershipsQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<ListStorefrontMembershipsQuery, ListStorefrontMembershipsResponse>
{
    public async Task<ListStorefrontMembershipsResponse> Handle(
        ListStorefrontMembershipsQuery request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountHandlerSupport.RequireAccountAsync(
            dbContext,
            request.Identity,
            includeLinkedIdentities: false,
            cancellationToken);
        var (page, pageSize, skip) = CustomerAccountPagination.Normalize(request.Page, request.PageSize);
        var query = dbContext.StorefrontMemberships
            .AsNoTracking()
            .Where(membership => membership.CustomerAccountId == account.Id)
            .OrderByDescending(membership => membership.LastActivityAt)
            .ThenBy(membership => membership.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip(skip).Take(pageSize).ToListAsync(cancellationToken);

        var response = new ListStorefrontMembershipsResponse
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
        response.Items.AddRange(items.Select(membership => membership.ToContractModel()));
        return response;
    }
}

public sealed class ListLinkedCustomerIdentitiesQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<ListLinkedCustomerIdentitiesQuery, ListLinkedCustomerIdentitiesResponse>
{
    public async Task<ListLinkedCustomerIdentitiesResponse> Handle(
        ListLinkedCustomerIdentitiesQuery request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountHandlerSupport.RequireAccountAsync(
            dbContext,
            request.Identity,
            includeLinkedIdentities: false,
            cancellationToken);
        var (page, pageSize, skip) = CustomerAccountPagination.Normalize(request.Page, request.PageSize);
        var query = dbContext.LinkedCustomerIdentities
            .AsNoTracking()
            .Where(identity => identity.CustomerAccountId == account.Id)
            .OrderByDescending(identity => identity.LinkedAt)
            .ThenBy(identity => identity.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip(skip).Take(pageSize).ToListAsync(cancellationToken);

        var response = new ListLinkedCustomerIdentitiesResponse
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
        response.Items.AddRange(items.Select(identity => identity.ToContractModel()));
        return response;
    }
}

public sealed class ListConsolidatedWishlistEntriesQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<ListConsolidatedWishlistEntriesQuery, ListConsolidatedWishlistEntriesResponse>
{
    public async Task<ListConsolidatedWishlistEntriesResponse> Handle(
        ListConsolidatedWishlistEntriesQuery request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountHandlerSupport.RequireAccountAsync(
            dbContext,
            request.Identity,
            includeLinkedIdentities: false,
            cancellationToken);
        var (page, pageSize, skip) = CustomerAccountPagination.Normalize(request.Page, request.PageSize);
        var query =
            from item in dbContext.WishlistItems.AsNoTracking()
            join profile in dbContext.CustomerProfiles.AsNoTracking() on item.CustomerId equals profile.Id
            where profile.CustomerAccountId == account.Id
            orderby item.AddedAt descending, item.Id
            select new WishlistProjection(item, profile.StorefrontId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip(skip).Take(pageSize).ToListAsync(cancellationToken);

        var response = new ListConsolidatedWishlistEntriesResponse
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
        response.Items.AddRange(items.Select(item =>
            item.Item.ToConsolidatedContractModel(item.StorefrontId)));
        return response;
    }

    private sealed record WishlistProjection(
        Domain.Entities.WishlistItem.WishlistItemEntity Item,
        Guid StorefrontId);
}

internal static class CustomerAccountPagination
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private const int MaximumPage = 1_000_000;

    public static (int Page, int PageSize, int Skip) Normalize(int requestedPage, int requestedPageSize)
    {
        var page = Math.Clamp(requestedPage <= 0 ? 1 : requestedPage, 1, MaximumPage);
        var pageSize = Math.Clamp(
            requestedPageSize <= 0 ? DefaultPageSize : requestedPageSize,
            1,
            MaximumPageSize);
        return (page, pageSize, (page - 1) * pageSize);
    }
}
