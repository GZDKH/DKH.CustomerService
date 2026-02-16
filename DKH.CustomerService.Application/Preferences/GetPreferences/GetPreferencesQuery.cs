using DKH.CustomerService.Contracts.Customer.Api.CustomerPreferencesManagement.v1;

namespace DKH.CustomerService.Application.Preferences.GetPreferences;

public sealed record GetPreferencesQuery(Guid StorefrontId, string UserId)
    : IRequest<GetPreferencesResponse>;
