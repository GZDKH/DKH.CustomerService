using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Wishlists.GetWishlistCount;

public sealed record GetWishlistCountQuery(Guid StorefrontId, string TelegramUserId)
    : IRequest<GetWishlistCountResponse>;
