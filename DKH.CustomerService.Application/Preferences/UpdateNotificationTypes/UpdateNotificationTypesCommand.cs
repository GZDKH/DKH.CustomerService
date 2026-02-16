using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Preferences.UpdateNotificationTypes;

public sealed record UpdateNotificationTypesCommand(
    Guid StorefrontId,
    string UserId,
    bool? OrderStatusUpdates,
    bool? PromotionalOffers)
    : IRequest<UpdateNotificationTypesResponse>;
