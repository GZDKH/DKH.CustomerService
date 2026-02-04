using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Wishlists.CheckProductInWishlist;

public sealed record CheckProductInWishlistQuery(
    Guid StorefrontId,
    string TelegramUserId,
    Guid ProductId,
    Guid? ProductSkuId)
    : IRequest<CheckProductInWishlistResponse>;
