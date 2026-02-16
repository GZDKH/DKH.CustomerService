using DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1;

namespace DKH.CustomerService.Application.Wishlists.GetWishlistCount;

public class GetWishlistCountQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<GetWishlistCountQuery, GetWishlistCountResponse>
{
    public async Task<GetWishlistCountResponse> Handle(GetWishlistCountQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
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
