using DKH.CustomerService.Application.ExternalIdentities.DeleteIdentity;
using DKH.CustomerService.Application.ExternalIdentities.FindByExternalIdentity;
using DKH.CustomerService.Application.ExternalIdentities.LinkIdentity;
using DKH.CustomerService.Application.ExternalIdentities.ListIdentities;
using DKH.CustomerService.Application.ExternalIdentities.MergeProfiles;
using DKH.CustomerService.Application.ExternalIdentities.PermanentlyDeleteIdentity;
using DKH.CustomerService.Application.ExternalIdentities.RestoreIdentity;
using DKH.CustomerService.Application.ExternalIdentities.UnlinkIdentity;
using DKH.CustomerService.Application.Mappers;
using DKH.Platform.Authorization.ResourceAccess;
using DKH.Platform.Authorization.ResourceAccess.Attributes;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Grpc.Extensions;
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

    [RequireResourceAccess("customer", ResourceAccessPermissions.Update)]
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

    [RequireResourceAccess("customer", ResourceAccessPermissions.Update)]
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

    [RequireResourceAccess("customer", ResourceAccessPermissions.Delete)]
    public override async Task<Empty> DeleteIdentity(
        ContractsServices.DeleteIdentityRequest request,
        ServerCallContext context)
    {
        var identityId = request.IdentityId.ToGuid();
        await _mediator.Send(new DeleteIdentityCommand(identityId), context.CancellationToken);
        return new Empty();
    }

    public override async Task<ContractsServices.ListIdentitiesResponse> ListIdentities(
        ContractsServices.ListIdentitiesRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await _mediator.Send(
            new ListIdentitiesQuery(
                storefrontId,
                request.UserId,
                request.HasSoftDeleteFilter
                    ? request.SoftDeleteFilter.ToDomain()
                    : Platform.Domain.Enums.PlatformSoftDeleteFilter.ActiveOnly),
            context.CancellationToken);
    }

    [RequireResourceAccess("customer", ResourceAccessPermissions.Read)]
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

    [RequireResourceAccess("customer", ResourceAccessPermissions.Update)]
    public override async Task<ContractsModels.CustomerProfile.v1.CustomerProfileModel> MergeProfiles(
        ContractsServices.MergeProfilesRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await _mediator.Send(
            new MergeProfilesCommand(
                storefrontId,
                request.SourceUserId,
                request.TargetUserId),
            context.CancellationToken);
    }

    [RequireResourceAccess("customer", ResourceAccessPermissions.Update)]
    public override async Task<ContractsModels.ExternalIdentity.v1.ExternalIdentityModel> RestoreIdentity(
        ContractsServices.RestoreIdentityRequest request,
        ServerCallContext context)
    {
        var identityId = request.IdentityId.ToGuid();
        var entity = await _mediator.Send(new RestoreIdentityCommand(identityId), context.CancellationToken);
        return entity.ToContractModel();
    }

    [RequireResourceAccess("customer", ResourceAccessPermissions.Delete)]
    public override async Task<Empty> PermanentlyDeleteIdentity(
        ContractsServices.PermanentlyDeleteIdentityRequest request,
        ServerCallContext context)
    {
        var identityId = request.IdentityId.ToGuid();
        await _mediator.Send(new PermanentlyDeleteIdentityCommand(identityId), context.CancellationToken);
        return new Empty();
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
