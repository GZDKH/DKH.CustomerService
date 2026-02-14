using DKH.CustomerService.Application.Admin.BlockCustomer;
using DKH.CustomerService.Application.Admin.ListCustomers;
using DKH.CustomerService.Application.Admin.SearchCustomers;
using DKH.CustomerService.Application.Admin.UnblockCustomer;
using DKH.CustomerService.Contracts.Services.V1;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Identity;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;

namespace DKH.CustomerService.Api.Services;

public class CustomerCrudGrpcService(
    IMediator mediator,
    IPlatformStorefrontContext storefrontContext,
    IPlatformCurrentUser currentUser)
    : CustomerCrudService.CustomerCrudServiceBase
{
    public override async Task<SearchCustomersResponse> SearchCustomers(
        SearchCustomersRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        var apiResponse = await mediator.Send(
            new SearchCustomersQuery(
                storefrontId,
                request.Query,
                request.Pagination?.Page ?? 1,
                request.Pagination?.PageSize ?? 10),
            context.CancellationToken);

        return MapToServicesV1(apiResponse);
    }

    public override async Task<ListCustomersResponse> ListCustomers(
        ListCustomersRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        var apiResponse = await mediator.Send(
            new ListCustomersQuery(
                storefrontId,
                request.Pagination?.Page ?? 1,
                request.Pagination?.PageSize ?? 10,
                request.SortBy,
                request.SortDescending),
            context.CancellationToken);

        return MapToServicesV1(apiResponse);
    }

    public override Task<GetCustomerStatsResponse> GetCustomerStats(
        GetCustomerStatsRequest request,
        ServerCallContext context)
    {
        // TODO: Implement GetCustomerStats
        return Task.FromResult(new GetCustomerStatsResponse());
    }

    public override async Task<BlockCustomerResponse> BlockCustomer(
        BlockCustomerRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var apiResponse = await mediator.Send(
            new BlockCustomerCommand(
                storefrontId,
                request.TelegramUserId,
                request.Reason,
                currentUser.Name!),
            context.CancellationToken);

        return MapToServicesV1(apiResponse);
    }

    public override async Task<UnblockCustomerResponse> UnblockCustomer(
        UnblockCustomerRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var apiResponse = await mediator.Send(
            new UnblockCustomerCommand(storefrontId, request.TelegramUserId),
            context.CancellationToken);

        return MapToServicesV1(apiResponse);
    }

    public override Task<SuspendCustomerResponse> SuspendCustomer(
        SuspendCustomerRequest request,
        ServerCallContext context)
    {
        // TODO: Implement SuspendCustomer
        return Task.FromResult(new SuspendCustomerResponse { Success = false });
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

    private Guid? ResolveStorefrontIdOptional(GuidValue? requestStorefrontId)
    {
        if (requestStorefrontId is not null)
        {
            return requestStorefrontId.ToGuid();
        }

        // For admin operations, return context storefront if available, or null (all storefronts)
        return storefrontContext.StorefrontId;
    }

    // Mappers: Api.V1 -> Services.V1
    private static SearchCustomersResponse MapToServicesV1(Contracts.Api.V1.SearchCustomersResponse apiResponse)
    {
        return new SearchCustomersResponse
        {
            Customers = { apiResponse.Customers },
            Pagination = apiResponse.Pagination
        };
    }

    private static ListCustomersResponse MapToServicesV1(Contracts.Api.V1.ListCustomersResponse apiResponse)
    {
        return new ListCustomersResponse
        {
            Customers = { apiResponse.Customers },
            Pagination = apiResponse.Pagination
        };
    }

    private static BlockCustomerResponse MapToServicesV1(Contracts.Api.V1.BlockCustomerResponse apiResponse)
    {
        return new BlockCustomerResponse
        {
            Success = apiResponse.Success,
            Profile = apiResponse.Profile
        };
    }

    private static UnblockCustomerResponse MapToServicesV1(Contracts.Api.V1.UnblockCustomerResponse apiResponse)
    {
        return new UnblockCustomerResponse
        {
            Success = apiResponse.Success,
            Profile = apiResponse.Profile
        };
    }
}
