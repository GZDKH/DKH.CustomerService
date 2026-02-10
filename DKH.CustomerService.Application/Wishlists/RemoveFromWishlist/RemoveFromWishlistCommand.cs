using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Wishlists.RemoveFromWishlist;

public sealed record RemoveFromWishlistCommand(
    Guid StorefrontId,
    string TelegramUserId,
    Guid ProductId,
    Guid? ProductSkuId)
    : IRequest<RemoveFromWishlistResponse>;
