using DKH.CustomerService.Application.ProductCollection.AddToCollection;
using DKH.CustomerService.Application.ProductCollection.GetCollection;
using DKH.CustomerService.Application.ProductCollection.GetCollectionItem;
using DKH.CustomerService.Application.ProductCollection.RemoveFromCollection;
using DKH.CustomerService.Application.ProductCollection.UpdateCollectionItem;
using DKH.CustomerService.Contracts.Customer.Api.ProductCollection.v1;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.ProductCollection.v1.ProductCollectionService;

namespace DKH.CustomerService.Api.Services;

// TODO(olac-phase4-caller-binding): per-row binding deferred to Phase 4
//   via ADR-025a D2 [RequireCallerMatchesClaim("UserId")] once Platform
//   1.x with D2 lands here.
[Authorize(Policy = CustomerServiceAuthorizationPolicies.CustomerAccess)]
public class ProductCollectionGrpcService(IMediator mediator)
    : ContractsService.ProductCollectionServiceBase
{
    public override async Task<ProductCollectionItemModel> AddToCollection(AddToCollectionRequest request, ServerCallContext context)
    {
        var customerId = request.CustomerId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Customer ID is required"));

        var productId = request.ProductId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Product ID is required"));

        var productSkuId = request.ProductSkuId?.ToGuid();

        return await mediator.Send(
            new AddToCollectionCommand(
                customerId,
                productId,
                productSkuId,
                request.Status,
                request.Notes,
                request.Rating),
            context.CancellationToken);
    }

    public override async Task<ProductCollectionItemModel> UpdateCollectionItem(UpdateCollectionItemRequest request, ServerCallContext context)
    {
        var itemId = request.ItemId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Item ID is required"));

        var customerId = request.CustomerId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Customer ID is required"));

        ProductCollectionStatus? status = request.Status != ProductCollectionStatus.Unspecified
            ? request.Status
            : null;

        return await mediator.Send(
            new UpdateCollectionItemCommand(
                itemId,
                customerId,
                status,
                request.Notes,
                request.Rating),
            context.CancellationToken);
    }

    public override async Task<Empty> RemoveFromCollection(RemoveFromCollectionRequest request, ServerCallContext context)
    {
        var itemId = request.ItemId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Item ID is required"));

        var customerId = request.CustomerId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Customer ID is required"));

        return await mediator.Send(
            new RemoveFromCollectionCommand(itemId, customerId),
            context.CancellationToken);
    }

    public override async Task<ProductCollectionListModel> GetCollection(GetCollectionRequest request, ServerCallContext context)
    {
        var customerId = request.CustomerId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Customer ID is required"));

        ProductCollectionStatus? statusFilter = request.StatusFilter != ProductCollectionStatus.Unspecified
            ? request.StatusFilter
            : null;

        return await mediator.Send(
            new GetCollectionQuery(
                customerId,
                statusFilter,
                request.Pagination?.Page ?? 1,
                request.Pagination?.PageSize ?? 10),
            context.CancellationToken);
    }

    public override async Task<ProductCollectionItemModel> GetCollectionItem(GetCollectionItemRequest request, ServerCallContext context)
    {
        var customerId = request.CustomerId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Customer ID is required"));

        var productId = request.ProductId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Product ID is required"));

        return await mediator.Send(
            new GetCollectionItemQuery(customerId, productId),
            context.CancellationToken);
    }
}
