using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Preferences.UpdateNotificationTypes;

public sealed record UpdateNotificationTypesCommand(
    Guid StorefrontId,
    string TelegramUserId,
    bool? OrderStatusUpdates,
    bool? PromotionalOffers)
    : IRequest<UpdateNotificationTypesResponse>;
