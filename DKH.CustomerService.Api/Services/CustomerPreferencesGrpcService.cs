using DKH.CustomerService.Application.Preferences.GetPreferences;
using DKH.CustomerService.Application.Preferences.UpdateNotificationChannels;
using DKH.CustomerService.Application.Preferences.UpdateNotificationTypes;
using DKH.CustomerService.Application.Preferences.UpdatePreferences;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using ContractsService = DKH.CustomerService.Contracts.Api.V1.CustomerPreferencesService;

namespace DKH.CustomerService.Api.Services;

public class CustomerPreferencesGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext)
    : ContractsService.CustomerPreferencesServiceBase
{
    public override async Task<GetPreferencesResponse> GetPreferences(GetPreferencesRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new GetPreferencesQuery(storefrontId, request.TelegramUserId), context.CancellationToken);
    }

    public override async Task<UpdatePreferencesResponse> UpdatePreferences(UpdatePreferencesRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new UpdatePreferencesCommand(
                storefrontId,
                request.TelegramUserId,
                request.HasPreferredLanguage ? request.PreferredLanguage : null,
                request.HasPreferredCurrency ? request.PreferredCurrency : null),
            context.CancellationToken);
    }

    public override async Task<UpdateNotificationChannelsResponse> UpdateNotificationChannels(UpdateNotificationChannelsRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new UpdateNotificationChannelsCommand(
                storefrontId,
                request.TelegramUserId,
                request.HasEmailNotificationsEnabled ? request.EmailNotificationsEnabled : null,
                request.HasTelegramNotificationsEnabled ? request.TelegramNotificationsEnabled : null,
                request.HasSmsNotificationsEnabled ? request.SmsNotificationsEnabled : null),
            context.CancellationToken);
    }

    public override async Task<UpdateNotificationTypesResponse> UpdateNotificationTypes(UpdateNotificationTypesRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new UpdateNotificationTypesCommand(
                storefrontId,
                request.TelegramUserId,
                request.HasOrderStatusUpdates ? request.OrderStatusUpdates : null,
                request.HasPromotionalOffers ? request.PromotionalOffers : null),
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
