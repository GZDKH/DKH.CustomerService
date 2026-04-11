using DKH.CustomerService.Application.Admin.BlockCustomer;
using DKH.CustomerService.Application.Admin.GetCustomerStats;
using DKH.CustomerService.Application.Admin.ListCustomers;
using DKH.CustomerService.Application.Admin.SearchCustomers;
using DKH.CustomerService.Application.Admin.UnblockCustomer;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Application.Profiles.PermanentlyDeleteProfile;
using DKH.CustomerService.Application.Profiles.RestoreProfile;
using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Grpc.Extensions;
using DKH.Platform.Identity;
using DKH.Platform.MultiTenancy;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace DKH.CustomerService.Api.Services;

[Authorize(Policy = CustomerServiceAuthorizationPolicies.CustomerAccess)]
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
        return await mediator.Send(
            new SearchCustomersQuery(
                storefrontId,
                request.Query,
                request.Pagination?.Page ?? 1,
                request.Pagination?.PageSize ?? 10),
            context.CancellationToken);
    }

    public override async Task<ListCustomersResponse> ListCustomers(
        ListCustomersRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        return await mediator.Send(
            new ListCustomersQuery(
                storefrontId,
                request.Pagination?.Page ?? 1,
                request.Pagination?.PageSize ?? 10,
                request.SortBy,
                request.SortDescending,
                request.HasSoftDeleteFilter
                    ? request.SoftDeleteFilter.ToDomain()
                    : Platform.Domain.Enums.PlatformSoftDeleteFilter.ActiveOnly),
            context.CancellationToken);
    }

    public override async Task<GetCustomerStatsResponse> GetCustomerStats(
        GetCustomerStatsRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontIdOptional(request.StorefrontId);
        return await mediator.Send(
            new GetCustomerStatsQuery(storefrontId, request.UserId),
            context.CancellationToken);
    }

    public override async Task<BlockCustomerResponse> BlockCustomer(
        BlockCustomerRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new BlockCustomerCommand(
                storefrontId,
                request.UserId,
                request.Reason,
                currentUser.Name!),
            context.CancellationToken);
    }

    public override async Task<UnblockCustomerResponse> UnblockCustomer(
        UnblockCustomerRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new UnblockCustomerCommand(storefrontId, request.UserId),
            context.CancellationToken);
    }

    public override Task<SuspendCustomerResponse> SuspendCustomer(
        SuspendCustomerRequest request,
        ServerCallContext context)
    {
        // TODO: Implement SuspendCustomer
        return Task.FromResult(new SuspendCustomerResponse { Success = false });
    }

    public override async Task<Contracts.Customer.Models.CustomerProfile.v1.CustomerProfileModel> RestoreCustomerProfile(
        RestoreCustomerProfileRequest request,
        ServerCallContext context)
    {
        var profileId = request.ProfileId.ToGuid();
        var entity = await mediator.Send(new RestoreProfileCommand(profileId), context.CancellationToken);
        return entity.ToContractModel();
    }

    public override async Task<Empty> PermanentlyDeleteCustomerProfile(
        PermanentlyDeleteCustomerProfileRequest request,
        ServerCallContext context)
    {
        var profileId = request.ProfileId.ToGuid();
        await mediator.Send(new PermanentlyDeleteProfileCommand(profileId), context.CancellationToken);
        return new Empty();
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
}
