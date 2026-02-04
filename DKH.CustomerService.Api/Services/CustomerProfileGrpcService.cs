using DKH.CustomerService.Application.Profiles.DeleteProfile;
using DKH.CustomerService.Application.Profiles.GetOrCreateProfile;
using DKH.CustomerService.Application.Profiles.GetProfile;
using DKH.CustomerService.Application.Profiles.UpdateProfile;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using ContractsService = DKH.CustomerService.Contracts.Api.V1.CustomerProfileService;

namespace DKH.CustomerService.Api.Services;

public class CustomerProfileGrpcService(IMediator mediator, IStorefrontContext storefrontContext)
    : ContractsService.CustomerProfileServiceBase
{
    public override async Task<GetProfileResponse> GetProfile(GetProfileRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new GetProfileQuery(storefrontId, request.TelegramUserId), context.CancellationToken);
    }

    public override async Task<GetOrCreateProfileResponse> GetOrCreateProfile(GetOrCreateProfileRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new GetOrCreateProfileCommand(
                storefrontId,
                request.TelegramUserId,
                request.FirstName,
                request.LastName,
                request.Username,
                request.PhotoUrl,
                request.LanguageCode),
            context.CancellationToken);
    }

    public override async Task<UpdateProfileResponse> UpdateProfile(UpdateProfileRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new UpdateProfileCommand(
                storefrontId,
                request.TelegramUserId,
                request.HasFirstName ? request.FirstName : null,
                request.HasLastName ? request.LastName : null,
                request.HasPhone ? request.Phone : null,
                request.HasEmail ? request.Email : null,
                request.HasLanguageCode ? request.LanguageCode : null),
            context.CancellationToken);
    }

    public override async Task<DeleteProfileResponse> DeleteProfile(DeleteProfileRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new DeleteProfileCommand(storefrontId, request.TelegramUserId, request.HardDelete),
            context.CancellationToken);
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
