using DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1;

namespace DKH.CustomerService.Application.Wishlists.GetWishlistCount;

public sealed record GetWishlistCountQuery(Guid StorefrontId, string UserId)
    : IRequest<GetWishlistCountResponse>;
