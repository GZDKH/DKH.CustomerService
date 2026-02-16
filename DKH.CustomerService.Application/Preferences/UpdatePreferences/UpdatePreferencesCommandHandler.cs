using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Preferences.UpdatePreferences;

public class UpdatePreferencesCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UpdatePreferencesCommand, UpdatePreferencesResponse>
{
    public async Task<UpdatePreferencesResponse> Handle(UpdatePreferencesCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
        {
            profile.Preferences.UpdateLanguage(request.PreferredLanguage);
        }

        if (!string.IsNullOrWhiteSpace(request.PreferredCurrency))
        {
            profile.Preferences.UpdateCurrency(request.PreferredCurrency);
        }

        await repository.UpdateAsync(profile, cancellationToken);

        return new UpdatePreferencesResponse
        {
            Preferences = profile.Preferences.ToContractModel(profile.Id),
        };
    }
}
