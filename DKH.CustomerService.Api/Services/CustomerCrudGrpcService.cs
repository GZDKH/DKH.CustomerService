using DKH.CustomerService.Application.Admin.BlockCustomer;
using DKH.CustomerService.Application.Admin.ListCustomers;
using DKH.CustomerService.Application.Admin.SearchCustomers;
using DKH.CustomerService.Application.Admin.UnblockCustomer;
using DKH.Platform.Grpc.Common.Types;
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
        var page = request.Pagination?.Page ?? 1;
        var pageSize = request.Pagination?.PageSize ?? 10;

        var result = await _mediator.Send(
            new SearchCustomersQuery(storefrontId, request.Query, page, pageSize),
            context.CancellationToken);

        // Map from Api.V1 to Services.V1 namespace
        var response = new ContractsServices.SearchCustomersResponse
        {
            Pagination = result.Pagination
        };
        response.Customers.AddRange(result.Customers);

        return response;
    }

    public override async Task<ContractsServices.ListCustomersResponse> ListCustomers(
        ContractsServices.ListCustomersRequest request,
        ServerCallContext context)
    {
        // For admin operations, storefront_id is optional (null means all storefronts)
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        var page = request.Pagination?.Page ?? 1;
        var pageSize = request.Pagination?.PageSize ?? 10;

        var result = await _mediator.Send(
            new ListCustomersQuery(storefrontId, page, pageSize, request.SortBy, request.SortDescending),
            context.CancellationToken);

        // Map from Api.V1 to Services.V1 namespace
        var response = new ContractsServices.ListCustomersResponse
        {
            Pagination = result.Pagination
        };
        response.Customers.AddRange(result.Customers);

        return response;
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
        var storefrontId = request.StorefrontId is not null
            ? request.StorefrontId.ToGuid()
            : throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required for blocking"));

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
        var storefrontId = request.StorefrontId is not null
            ? request.StorefrontId.ToGuid()
            : throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required for unblocking"));

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

    private Guid? ResolveStorefrontIdOptional(GuidValue? requestStorefrontId)
    {
        // For admin operations, storefront_id is optional (null means all storefronts)
        if (requestStorefrontId is not null)
        {
            return requestStorefrontId.ToGuid();
        }

        // Return storefront from context if available, otherwise null (admin mode)
        return _storefrontContext.StorefrontId;
    }
}
