using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Preferences.GetPreferences;

public sealed record GetPreferencesQuery(Guid StorefrontId, string UserId)
    : IRequest<GetPreferencesResponse>;
