using DKH.CustomerService.Application.Admin.BlockCustomer;
using DKH.CustomerService.Application.Admin.ListCustomers;
using DKH.CustomerService.Application.Admin.SearchCustomers;
using DKH.CustomerService.Application.Admin.UnblockCustomer;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.Platform.Identity;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using ContractsService = DKH.CustomerService.Contracts.Api.V1.CustomerAdminService;

namespace DKH.CustomerService.Api.Services;

public class CustomerAdminGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext, IPlatformCurrentUser currentUser)
    : ContractsService.CustomerAdminServiceBase
{
    public override async Task<SearchCustomersResponse> SearchCustomers(SearchCustomersRequest request, ServerCallContext context)
    {
        // For admin operations, storefront_id is optional (null means all storefronts)
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        return await mediator.Send(
            new SearchCustomersQuery(storefrontId, request.Query, request.Page, request.PageSize),
            context.CancellationToken);
    }

    public override async Task<ListCustomersResponse> ListCustomers(ListCustomersRequest request, ServerCallContext context)
    {
        // For admin operations, storefront_id is optional (null means all storefronts)
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        return await mediator.Send(
            new ListCustomersQuery(storefrontId, request.Page, request.PageSize, request.SortBy, request.SortDescending),
            context.CancellationToken);
    }

    public override Task<GetCustomerStatsResponse> GetCustomerStats(GetCustomerStatsRequest request, ServerCallContext context)
    {
        return Task.FromResult(new GetCustomerStatsResponse());
    }

    public override async Task<BlockCustomerResponse> BlockCustomer(BlockCustomerRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new BlockCustomerCommand(storefrontId, request.TelegramUserId, request.Reason, currentUser.Name!),
            context.CancellationToken);
    }

    public override async Task<UnblockCustomerResponse> UnblockCustomer(UnblockCustomerRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new UnblockCustomerCommand(storefrontId, request.TelegramUserId), context.CancellationToken);
    }

    public override Task<SuspendCustomerResponse> SuspendCustomer(SuspendCustomerRequest request, ServerCallContext context)
    {
        return Task.FromResult(new SuspendCustomerResponse { Success = false });
    }

    public override Task<ExportCustomerDataResponse> ExportCustomerData(ExportCustomerDataRequest request, ServerCallContext context)
    {
        return Task.FromResult(new ExportCustomerDataResponse());
    }

    public override Task<DeleteCustomerDataResponse> DeleteCustomerData(DeleteCustomerDataRequest request, ServerCallContext context)
    {
        return Task.FromResult(new DeleteCustomerDataResponse { Success = false });
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

    private Guid? ResolveStorefrontIdOptional(string requestStorefrontId)
    {
        // For admin operations, storefront_id is optional (null means all storefronts)
        if (!string.IsNullOrWhiteSpace(requestStorefrontId) && Guid.TryParse(requestStorefrontId, out var parsed))
        {
            return parsed;
        }

        // Return storefront from context if available, otherwise null (admin mode)
        return storefrontContext.StorefrontId;
    }
}
