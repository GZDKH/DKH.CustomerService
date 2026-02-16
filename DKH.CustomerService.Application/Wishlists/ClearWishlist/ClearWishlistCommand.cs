using DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1;

namespace DKH.CustomerService.Application.Wishlists.ClearWishlist;

public sealed record ClearWishlistCommand(Guid StorefrontId, string UserId)
    : IRequest<ClearWishlistResponse>;
