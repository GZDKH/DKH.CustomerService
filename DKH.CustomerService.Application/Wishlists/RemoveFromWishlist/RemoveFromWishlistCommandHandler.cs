using DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1;

namespace DKH.CustomerService.Application.Wishlists.RemoveFromWishlist;

public class RemoveFromWishlistCommandHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<RemoveFromWishlistCommand, RemoveFromWishlistResponse>
{
    public async Task<RemoveFromWishlistResponse> Handle(RemoveFromWishlistCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
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
