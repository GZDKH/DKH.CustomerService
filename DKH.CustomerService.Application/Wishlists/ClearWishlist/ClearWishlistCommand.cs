using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Wishlists.ClearWishlist;

public sealed record ClearWishlistCommand(Guid StorefrontId, string UserId)
    : IRequest<ClearWishlistResponse>;
