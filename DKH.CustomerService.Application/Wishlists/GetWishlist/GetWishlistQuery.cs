using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Wishlists.GetWishlist;

public sealed record GetWishlistQuery(Guid StorefrontId, string TelegramUserId, int Page, int PageSize)
    : IRequest<GetWishlistResponse>;
