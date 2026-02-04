using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Api.V1;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Application.Wishlists.GetWishlistCount;

public class GetWishlistCountQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<GetWishlistCountQuery, GetWishlistCountResponse>
{
    public async Task<GetWishlistCountResponse> Handle(GetWishlistCountQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken);

        if (profile is null)
        {
            return new GetWishlistCountResponse { Count = 0 };
        }

        var count = await dbContext.WishlistItems
            .Where(w => w.CustomerId == profile.Id)
            .CountAsync(cancellationToken);

        return new GetWishlistCountResponse { Count = count };
    }
}
