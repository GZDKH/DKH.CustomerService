using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Wishlists.ClearWishlist;

public class ClearWishlistCommandHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<ClearWishlistCommand, ClearWishlistResponse>
{
    public async Task<ClearWishlistResponse> Handle(ClearWishlistCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken);

        if (profile is null)
        {
            return new ClearWishlistResponse { ItemsRemoved = 0 };
        }

        var items = await dbContext.WishlistItems
            .Where(w => w.CustomerId == profile.Id)
            .ToListAsync(cancellationToken);

        var count = items.Count;
        dbContext.WishlistItems.RemoveRange(items);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ClearWishlistResponse { ItemsRemoved = count };
    }
}
