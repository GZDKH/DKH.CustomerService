using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Addresses.GetAddress;

public class GetAddressQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<GetAddressQuery, GetAddressResponse>
{
    public async Task<GetAddressResponse> Handle(GetAddressQuery request, CancellationToken cancellationToken)
    {
        var address = await dbContext.CustomerAddresses.FindAsync([request.AddressId], cancellationToken);

        if (address is null)
        {
            return new GetAddressResponse { Found = false };
        }

        return new GetAddressResponse
        {
            Address = address.ToContractModel(),
            Found = true,
        };
    }
}
