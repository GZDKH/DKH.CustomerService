using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Preferences.UpdatePreferences;

public sealed record UpdatePreferencesCommand(
    Guid StorefrontId,
    string UserId,
    string? PreferredLanguage,
    string? PreferredCurrency)
    : IRequest<UpdatePreferencesResponse>;
