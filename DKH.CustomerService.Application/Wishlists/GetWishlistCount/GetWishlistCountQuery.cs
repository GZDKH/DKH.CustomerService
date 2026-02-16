using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Wishlists.GetWishlistCount;

public sealed record GetWishlistCountQuery(Guid StorefrontId, string UserId)
    : IRequest<GetWishlistCountResponse>;
