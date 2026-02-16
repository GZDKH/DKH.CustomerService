using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Addresses.ListAddresses;

public class ListAddressesQueryHandler(ICustomerRepository repository)
    : IRequestHandler<ListAddressesQuery, ListAddressesResponse>
{
    public async Task<ListAddressesResponse> Handle(ListAddressesQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdWithAddressesAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken);

        var response = new ListAddressesResponse();

        if (profile is not null)
        {
            response.Addresses.AddRange(profile.Addresses.Select(a => a.ToContractModel()));
        }

        return response;
    }
}
