using DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1;

namespace DKH.CustomerService.Application.Wishlists.CheckProductInWishlist;

public sealed record CheckProductInWishlistQuery(
    Guid StorefrontId,
    string UserId,
    Guid ProductId,
    Guid? ProductSkuId)
    : IRequest<CheckProductInWishlistResponse>;
