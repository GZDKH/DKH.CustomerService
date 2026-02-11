using DKH.CustomerService.Application.Admin.BlockCustomer;
using DKH.CustomerService.Application.Admin.ListCustomers;
using DKH.CustomerService.Application.Admin.SearchCustomers;
using DKH.CustomerService.Application.Admin.UnblockCustomer;
using DKH.Platform.Identity;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using ContractsServices = DKH.CustomerService.Contracts.Services.V1;

namespace DKH.CustomerService.Api.Services;

/// <summary>
/// CustomerCrudService implementation for admin-level CRUD operations.
/// All methods accept optional storefront_id - when null/empty, operates across all storefronts.
/// </summary>
public sealed class CustomerCrudGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext, IPlatformCurrentUser currentUser)
    : ContractsServices.CustomerCrudService.CustomerCrudServiceBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IPlatformStorefrontContext _storefrontContext = storefrontContext;

    public override async Task<ContractsServices.SearchCustomersResponse> SearchCustomers(
        ContractsServices.SearchCustomersRequest request,
        ServerCallContext context)
    {
        // For admin operations, storefront_id is optional (null means all storefronts)
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        var result = await _mediator.Send(
            new SearchCustomersQuery(storefrontId, request.Query, request.Page, request.PageSize),
            context.CancellationToken);

        return new ContractsServices.SearchCustomersResponse
        {
            Customers = { result.Customers },
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasNextPage = result.Page < result.TotalPages,
            HasPreviousPage = result.Page > 1
        };
    }

    public override async Task<ContractsServices.ListCustomersResponse> ListCustomers(
        ContractsServices.ListCustomersRequest request,
        ServerCallContext context)
    {
        // For admin operations, storefront_id is optional (null means all storefronts)
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        var result = await _mediator.Send(
            new ListCustomersQuery(storefrontId, request.Page, request.PageSize, request.SortBy, request.SortDescending),
            context.CancellationToken);

        return new ContractsServices.ListCustomersResponse
        {
            Customers = { result.Customers },
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasNextPage = result.Page < result.TotalPages,
            HasPreviousPage = result.Page > 1
        };
    }

    public override Task<ContractsServices.GetCustomerStatsResponse> GetCustomerStats(
        ContractsServices.GetCustomerStatsRequest request,
        ServerCallContext context)
    {
        // TODO: Implement GetCustomerStats
        return Task.FromResult(new ContractsServices.GetCustomerStatsResponse());
    }

    public override async Task<ContractsServices.BlockCustomerResponse> BlockCustomer(
        ContractsServices.BlockCustomerRequest request,
        ServerCallContext context)
    {
        // For admin operations, storefront_id can be optional
        var storefrontId = string.IsNullOrWhiteSpace(request.StorefrontId)
            ? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required for blocking"))
            : Guid.Parse(request.StorefrontId);

        var result = await _mediator.Send(
            new BlockCustomerCommand(storefrontId, request.TelegramUserId, request.Reason, currentUser.Name!),
            context.CancellationToken);
        return new ContractsServices.BlockCustomerResponse
        {
            Success = result.Success,
            Profile = result.Profile
        };
    }

    public override async Task<ContractsServices.UnblockCustomerResponse> UnblockCustomer(
        ContractsServices.UnblockCustomerRequest request,
        ServerCallContext context)
    {
        // For admin operations, storefront_id can be optional
        var storefrontId = string.IsNullOrWhiteSpace(request.StorefrontId)
            ? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required for unblocking"))
            : Guid.Parse(request.StorefrontId);

        var result = await _mediator.Send(
            new UnblockCustomerCommand(storefrontId, request.TelegramUserId),
            context.CancellationToken);
        return new ContractsServices.UnblockCustomerResponse
        {
            Success = result.Success,
            Profile = result.Profile
        };
    }

    public override Task<ContractsServices.SuspendCustomerResponse> SuspendCustomer(
        ContractsServices.SuspendCustomerRequest request,
        ServerCallContext context)
    {
        // TODO: Implement SuspendCustomer
        return Task.FromResult(new ContractsServices.SuspendCustomerResponse { Success = false });
    }

    private Guid? ResolveStorefrontIdOptional(string requestStorefrontId)
    {
        // For admin operations, storefront_id is optional (null means all storefronts)
        if (!string.IsNullOrWhiteSpace(requestStorefrontId) && Guid.TryParse(requestStorefrontId, out var parsed))
        {
            return parsed;
        }

        // Return storefront from context if available, otherwise null (admin mode)
        return _storefrontContext.StorefrontId;
    }
}
