using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Wishlists.AddToWishlist;

public sealed record AddToWishlistCommand(
    Guid StorefrontId,
    string UserId,
    Guid ProductId,
    Guid? ProductSkuId,
    string? Note)
    : IRequest<AddToWishlistResponse>;
