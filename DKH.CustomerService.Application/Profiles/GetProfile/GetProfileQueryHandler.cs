using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Profiles.GetProfile;

public class GetProfileQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetProfileQuery, GetProfileResponse>
{
    public async Task<GetProfileResponse> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken);

        if (profile is null)
        {
            return new GetProfileResponse { Found = false };
        }

        return new GetProfileResponse
        {
            Profile = profile.ToContractModel(),
            Found = true,
        };
    }
}
