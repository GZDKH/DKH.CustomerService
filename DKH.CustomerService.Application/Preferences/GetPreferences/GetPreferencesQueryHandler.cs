using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.CustomerService.Contracts.Models.V1;

namespace DKH.CustomerService.Application.Preferences.GetPreferences;

public class GetPreferencesQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetPreferencesQuery, GetPreferencesResponse>
{
    public async Task<GetPreferencesResponse> Handle(GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken);

        if (profile is null)
        {
            return new GetPreferencesResponse
            {
                Preferences = new CustomerPreferences(),
            };
        }

        return new GetPreferencesResponse
        {
            Preferences = profile.Preferences.ToContractModel(profile.Id),
        };
    }
}
