using DKH.CustomerService.Application.ExternalIdentities.FindByExternalIdentity;
using DKH.CustomerService.Application.ExternalIdentities.LinkIdentity;
using DKH.CustomerService.Application.ExternalIdentities.ListIdentities;
using DKH.CustomerService.Application.ExternalIdentities.UnlinkIdentity;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using ContractsModels = DKH.CustomerService.Contracts.Customer.Models;
using ContractsServices = DKH.CustomerService.Contracts.Customer.Api.IdentityLinking.v1;

namespace DKH.CustomerService.Api.Services;

public sealed class IdentityLinkingGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext)
    : ContractsServices.IdentityLinkingService.IdentityLinkingServiceBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IPlatformStorefrontContext _storefrontContext = storefrontContext;

    public override async Task<ContractsModels.ExternalIdentity.v1.ExternalIdentityModel> LinkIdentity(
        ContractsServices.LinkIdentityRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await _mediator.Send(
            new LinkIdentityCommand(
                storefrontId,
                request.UserId,
                request.Provider,
                request.ProviderUserId,
                string.IsNullOrEmpty(request.Email) ? null : request.Email,
                string.IsNullOrEmpty(request.DisplayName) ? null : request.DisplayName,
                request.IsPrimary),
            context.CancellationToken);
    }

    public override async Task<Empty> UnlinkIdentity(
        ContractsServices.UnlinkIdentityRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        await _mediator.Send(
            new UnlinkIdentityCommand(
                storefrontId,
                request.UserId,
                request.IdentityId.ToGuid()),
            context.CancellationToken);
        return new Empty();
    }

    public override async Task<ContractsServices.ListIdentitiesResponse> ListIdentities(
        ContractsServices.ListIdentitiesRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await _mediator.Send(
            new ListIdentitiesQuery(storefrontId, request.UserId),
            context.CancellationToken);
    }

    public override async Task<ContractsModels.CustomerProfile.v1.CustomerProfileModel> FindByExternalIdentity(
        ContractsServices.FindByExternalIdentityRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await _mediator.Send(
            new FindByExternalIdentityQuery(
                storefrontId,
                request.Provider,
                request.ProviderUserId),
            context.CancellationToken);
    }

    public override Task<ContractsModels.CustomerProfile.v1.CustomerProfileModel> MergeProfiles(
        ContractsServices.MergeProfilesRequest request,
        ServerCallContext context)
    {
        // MergeProfiles will be implemented in Phase 4 (Issue #31)
        throw new RpcException(new Status(StatusCode.Unimplemented, "MergeProfiles will be implemented in Phase 4."));
    }

    private Guid ResolveStorefrontId(GuidValue? requestStorefrontId)
    {
        if (requestStorefrontId is not null)
        {
            return requestStorefrontId.ToGuid();
        }

        return _storefrontContext.StorefrontId
               ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required"));
    }
}
