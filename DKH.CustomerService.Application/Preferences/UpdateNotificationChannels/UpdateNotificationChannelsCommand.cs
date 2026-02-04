using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Preferences.UpdateNotificationChannels;

public sealed record UpdateNotificationChannelsCommand(
    Guid StorefrontId,
    string TelegramUserId,
    bool? EmailNotificationsEnabled,
    bool? TelegramNotificationsEnabled,
    bool? SmsNotificationsEnabled)
    : IRequest<UpdateNotificationChannelsResponse>;
