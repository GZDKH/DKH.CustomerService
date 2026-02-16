using DKH.CustomerService.Application.Common;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1;
using DKH.CustomerService.Contracts.Customer.Models.WishlistItem.v1;

namespace DKH.CustomerService.Application.Wishlists.GetWishlist;

public class GetWishlistQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<GetWishlistQuery, GetWishlistResponse>
{
    public async Task<GetWishlistResponse> Handle(GetWishlistQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken);

        if (profile is null)
        {
            return new GetWishlistResponse
            {
                Wishlist = new WishlistModel
                {
                    Pagination = PaginationHelper.CreateMetadata(0, 1, 10),
                },
            };
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.WishlistItems
            .Where(w => w.CustomerId == profile.Id)
            .OrderByDescending(w => w.AddedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var wishlist = new WishlistModel
        {
            Pagination = PaginationHelper.CreateMetadata(totalCount, page, pageSize),
        };
        wishlist.Items.AddRange(items.Select(i => i.ToContractModel()));

        return new GetWishlistResponse
        {
            Wishlist = wishlist,
        };
    }
}
