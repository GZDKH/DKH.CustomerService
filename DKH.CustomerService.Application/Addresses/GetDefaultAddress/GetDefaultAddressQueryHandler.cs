using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.GetDefaultAddress;

public class GetDefaultAddressQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<GetDefaultAddressQuery, GetDefaultAddressResponse>
{
    public async Task<GetDefaultAddressResponse> Handle(GetDefaultAddressQuery request, CancellationToken cancellationToken)
    {
        var address = await CustomerAddressOwnership.OwnedAddresses(dbContext, request.StorefrontId, request.UserId)
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (address is null)
        {
            return new GetDefaultAddressResponse { Found = false };
        }

        return new GetDefaultAddressResponse
        {
            Address = address.ToContractModel(),
            Found = true,
        };
    }
}
