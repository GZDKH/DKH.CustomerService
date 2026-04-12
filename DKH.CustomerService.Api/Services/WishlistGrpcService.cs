using DKH.CustomerService.Application.Wishlists.AddToWishlist;
using DKH.CustomerService.Application.Wishlists.CheckProductInWishlist;
using DKH.CustomerService.Application.Wishlists.ClearWishlist;
using DKH.CustomerService.Application.Wishlists.GetWishlist;
using DKH.CustomerService.Application.Wishlists.GetWishlistCount;
using DKH.CustomerService.Application.Wishlists.RemoveFromWishlist;
using DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.WishlistManagement.v1.WishlistManagementService;

namespace DKH.CustomerService.Api.Services;

public class WishlistGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext)
    : ContractsService.WishlistManagementServiceBase
{
    public override async Task<GetWishlistResponse> GetWishlist(GetWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new GetWishlistQuery(storefrontId, request.UserId, (request.Pagination?.Page ?? 1), (request.Pagination?.PageSize ?? 10)),
            context.CancellationToken);
    }

    public override async Task<AddToWishlistResponse> AddToWishlist(AddToWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var productId = request.ProductId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Product ID is required"));

        var productSkuId = request.ProductSkuId?.ToGuid();

        return await mediator.Send(
            new AddToWishlistCommand(storefrontId, request.UserId, productId, productSkuId, request.Note),
            context.CancellationToken);
    }

    public override async Task<RemoveFromWishlistResponse> RemoveFromWishlist(RemoveFromWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var productId = request.ProductId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Product ID is required"));

        var productSkuId = request.ProductSkuId?.ToGuid();

        return await mediator.Send(
            new RemoveFromWishlistCommand(storefrontId, request.UserId, productId, productSkuId),
            context.CancellationToken);
    }

    public override async Task<CheckProductInWishlistResponse> CheckProductInWishlist(CheckProductInWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var productId = request.ProductId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Product ID is required"));

        var productSkuId = request.ProductSkuId?.ToGuid();

        return await mediator.Send(
            new CheckProductInWishlistQuery(storefrontId, request.UserId, productId, productSkuId),
            context.CancellationToken);
    }

    public override async Task<ClearWishlistResponse> ClearWishlist(ClearWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new ClearWishlistCommand(storefrontId, request.UserId), context.CancellationToken);
    }

    public override async Task<GetWishlistCountResponse> GetWishlistCount(GetWishlistCountRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new GetWishlistCountQuery(storefrontId, request.UserId), context.CancellationToken);
    }

    private Guid ResolveStorefrontId(GuidValue? requestStorefrontId)
    {
        if (requestStorefrontId is not null)
        {
            return requestStorefrontId.ToGuid();
        }

        return storefrontContext.StorefrontId
               ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required"));
    }
}
