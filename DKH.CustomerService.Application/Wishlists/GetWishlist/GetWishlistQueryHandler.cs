using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.CustomerService.Contracts.Models.V1;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Application.Wishlists.GetWishlist;

public class GetWishlistQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<GetWishlistQuery, GetWishlistResponse>
{
    public async Task<GetWishlistResponse> Handle(GetWishlistQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken);

        if (profile is null)
        {
            return new GetWishlistResponse
            {
                Wishlist = new Wishlist { TotalCount = 0 },
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = 0,
            };
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.WishlistItems
            .Where(w => w.CustomerId == profile.Id)
            .OrderByDescending(w => w.AddedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var wishlist = new Wishlist { TotalCount = totalCount };
        wishlist.Items.AddRange(items.Select(i => i.ToContractModel()));

        return new GetWishlistResponse
        {
            Wishlist = wishlist,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
        };
    }
}
