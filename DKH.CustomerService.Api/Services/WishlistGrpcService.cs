using DKH.CustomerService.Application.Wishlists.AddToWishlist;
using DKH.CustomerService.Application.Wishlists.CheckProductInWishlist;
using DKH.CustomerService.Application.Wishlists.ClearWishlist;
using DKH.CustomerService.Application.Wishlists.GetWishlist;
using DKH.CustomerService.Application.Wishlists.GetWishlistCount;
using DKH.CustomerService.Application.Wishlists.RemoveFromWishlist;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using ContractsService = DKH.CustomerService.Contracts.Api.V1.WishlistService;

namespace DKH.CustomerService.Api.Services;

public class WishlistGrpcService(IMediator mediator, IStorefrontContext storefrontContext)
    : ContractsService.WishlistServiceBase
{
    public override async Task<GetWishlistResponse> GetWishlist(GetWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new GetWishlistQuery(storefrontId, request.TelegramUserId, request.Page, request.PageSize),
            context.CancellationToken);
    }

    public override async Task<AddToWishlistResponse> AddToWishlist(AddToWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        if (!Guid.TryParse(request.ProductId, out var productId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid product ID"));
        }

        Guid? productSkuId = null;
        if (!string.IsNullOrWhiteSpace(request.ProductSkuId) && Guid.TryParse(request.ProductSkuId, out var skuId))
        {
            productSkuId = skuId;
        }

        return await mediator.Send(
            new AddToWishlistCommand(storefrontId, request.TelegramUserId, productId, productSkuId, request.Note),
            context.CancellationToken);
    }

    public override async Task<RemoveFromWishlistResponse> RemoveFromWishlist(RemoveFromWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        if (!Guid.TryParse(request.ProductId, out var productId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid product ID"));
        }

        Guid? productSkuId = null;
        if (!string.IsNullOrWhiteSpace(request.ProductSkuId) && Guid.TryParse(request.ProductSkuId, out var skuId))
        {
            productSkuId = skuId;
        }

        return await mediator.Send(
            new RemoveFromWishlistCommand(storefrontId, request.TelegramUserId, productId, productSkuId),
            context.CancellationToken);
    }

    public override async Task<CheckProductInWishlistResponse> CheckProductInWishlist(CheckProductInWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        if (!Guid.TryParse(request.ProductId, out var productId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid product ID"));
        }

        Guid? productSkuId = null;
        if (!string.IsNullOrWhiteSpace(request.ProductSkuId) && Guid.TryParse(request.ProductSkuId, out var skuId))
        {
            productSkuId = skuId;
        }

        return await mediator.Send(
            new CheckProductInWishlistQuery(storefrontId, request.TelegramUserId, productId, productSkuId),
            context.CancellationToken);
    }

    public override async Task<ClearWishlistResponse> ClearWishlist(ClearWishlistRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new ClearWishlistCommand(storefrontId, request.TelegramUserId), context.CancellationToken);
    }

    public override async Task<GetWishlistCountResponse> GetWishlistCount(GetWishlistCountRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new GetWishlistCountQuery(storefrontId, request.TelegramUserId), context.CancellationToken);
    }

    private Guid ResolveStorefrontId(string requestStorefrontId)
    {
        if (!string.IsNullOrWhiteSpace(requestStorefrontId) && Guid.TryParse(requestStorefrontId, out var parsed))
        {
            return parsed;
        }

        return storefrontContext.StorefrontId
               ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required"));
    }
}
