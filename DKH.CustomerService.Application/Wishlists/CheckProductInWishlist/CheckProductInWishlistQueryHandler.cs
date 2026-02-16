using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Wishlists.CheckProductInWishlist;

public class CheckProductInWishlistQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<CheckProductInWishlistQuery, CheckProductInWishlistResponse>
{
    public async Task<CheckProductInWishlistResponse> Handle(CheckProductInWishlistQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken);

        if (profile is null)
        {
            return new CheckProductInWishlistResponse { InWishlist = false };
        }

        var item = await dbContext.WishlistItems
            .Where(w => w.CustomerId == profile.Id &&
                        w.ProductId == request.ProductId &&
                        w.ProductSkuId == request.ProductSkuId)
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return new CheckProductInWishlistResponse { InWishlist = false };
        }

        return new CheckProductInWishlistResponse
        {
            InWishlist = true,
            Item = item.ToContractModel(),
        };
    }
}
