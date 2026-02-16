using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Preferences.UpdateNotificationChannels;

public sealed record UpdateNotificationChannelsCommand(
    Guid StorefrontId,
    string UserId,
    bool? EmailNotificationsEnabled,
    bool? TelegramNotificationsEnabled,
    bool? SmsNotificationsEnabled)
    : IRequest<UpdateNotificationChannelsResponse>;
