using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Preferences.GetPreferences;

public sealed record GetPreferencesQuery(Guid StorefrontId, string TelegramUserId)
    : IRequest<GetPreferencesResponse>;
