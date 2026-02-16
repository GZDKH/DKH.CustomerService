using DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1;

namespace DKH.CustomerService.Application.Wishlists.GetWishlist;

public sealed record GetWishlistQuery(Guid StorefrontId, string UserId, int Page, int PageSize)
    : IRequest<GetWishlistResponse>;
