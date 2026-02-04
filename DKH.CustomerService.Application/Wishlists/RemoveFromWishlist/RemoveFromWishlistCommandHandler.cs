using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Api.V1;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Application.Wishlists.RemoveFromWishlist;

public class RemoveFromWishlistCommandHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<RemoveFromWishlistCommand, RemoveFromWishlistResponse>
{
    public async Task<RemoveFromWishlistResponse> Handle(RemoveFromWishlistCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken);

        if (profile is null)
        {
            return new RemoveFromWishlistResponse { Success = false };
        }

        var item = await dbContext.WishlistItems
            .Where(w => w.CustomerId == profile.Id &&
                        w.ProductId == request.ProductId &&
                        w.ProductSkuId == request.ProductSkuId)
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return new RemoveFromWishlistResponse { Success = false };
        }

        dbContext.WishlistItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RemoveFromWishlistResponse { Success = true };
    }
}
