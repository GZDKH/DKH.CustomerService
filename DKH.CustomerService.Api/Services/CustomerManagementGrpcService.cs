using DKH.CustomerService.Application.Profiles.DeleteProfile;
using DKH.CustomerService.Application.Profiles.GetOrCreateProfile;
using DKH.CustomerService.Application.Profiles.GetProfile;
using DKH.CustomerService.Application.Profiles.UpdateProfile;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using ContractsServices = DKH.CustomerService.Contracts.Services.V1;

namespace DKH.CustomerService.Api.Services;

/// <summary>
/// CustomerManagementService implementation for storefront-scoped customer profile operations.
/// All methods require mandatory storefront_id - operations are restricted to the specified storefront.
/// </summary>
public sealed class CustomerManagementGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext)
    : ContractsServices.CustomerManagementService.CustomerManagementServiceBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IPlatformStorefrontContext _storefrontContext = storefrontContext;

    public override async Task<ContractsServices.GetProfileResponse> GetProfile(
        ContractsServices.GetProfileRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var result = await _mediator.Send(new GetProfileQuery(storefrontId, request.TelegramUserId), context.CancellationToken);
        return new ContractsServices.GetProfileResponse
        {
            Profile = result.Profile,
            Found = result.Found
        };
    }

    public override async Task<ContractsServices.GetOrCreateProfileResponse> GetOrCreateProfile(
        ContractsServices.GetOrCreateProfileRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var result = await _mediator.Send(
            new GetOrCreateProfileCommand(
                storefrontId,
                request.TelegramUserId,
                request.FirstName,
                request.LastName,
                request.Username,
                request.PhotoUrl,
                request.LanguageCode),
            context.CancellationToken);
        return new ContractsServices.GetOrCreateProfileResponse
        {
            Profile = result.Profile,
            Created = result.Created
        };
    }

    public override async Task<ContractsServices.UpdateProfileResponse> UpdateProfile(
        ContractsServices.UpdateProfileRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var result = await _mediator.Send(
            new UpdateProfileCommand(
                storefrontId,
                request.TelegramUserId,
                request.HasFirstName ? request.FirstName : null,
                request.HasLastName ? request.LastName : null,
                request.HasPhone ? request.Phone : null,
                request.HasEmail ? request.Email : null,
                request.HasLanguageCode ? request.LanguageCode : null),
            context.CancellationToken);
        return new ContractsServices.UpdateProfileResponse
        {
            Profile = result.Profile
        };
    }

    public override async Task<ContractsServices.DeleteProfileResponse> DeleteProfile(
        ContractsServices.DeleteProfileRequest request,
        ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var result = await _mediator.Send(
            new DeleteProfileCommand(storefrontId, request.TelegramUserId, request.HardDelete),
            context.CancellationToken);
        return new ContractsServices.DeleteProfileResponse
        {
            Success = result.Success
        };
    }

    private Guid ResolveStorefrontId(string requestStorefrontId)
    {
        if (!string.IsNullOrWhiteSpace(requestStorefrontId) && Guid.TryParse(requestStorefrontId, out var parsed))
        {
            return parsed;
        }

        return _storefrontContext.StorefrontId
               ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required"));
    }
}
