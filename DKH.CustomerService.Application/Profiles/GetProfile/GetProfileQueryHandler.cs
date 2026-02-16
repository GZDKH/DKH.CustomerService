using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;

namespace DKH.CustomerService.Application.Profiles.GetProfile;

public class GetProfileQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetProfileQuery, GetProfileResponse>
{
    public async Task<GetProfileResponse> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
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
