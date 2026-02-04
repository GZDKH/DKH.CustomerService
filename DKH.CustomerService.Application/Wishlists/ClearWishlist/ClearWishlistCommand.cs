using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Wishlists.ClearWishlist;

public sealed record ClearWishlistCommand(Guid StorefrontId, string TelegramUserId)
    : IRequest<ClearWishlistResponse>;
