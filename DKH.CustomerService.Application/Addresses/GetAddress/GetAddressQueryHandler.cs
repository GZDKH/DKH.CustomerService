using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.GetAddress;

public class GetAddressQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<GetAddressQuery, GetAddressResponse>
{
    public async Task<GetAddressResponse> Handle(GetAddressQuery request, CancellationToken cancellationToken)
    {
        var address = await CustomerAddressOwnership.OwnedAddresses(dbContext, request.StorefrontId, request.UserId)
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == request.AddressId && !value.IsDeleted, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

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
