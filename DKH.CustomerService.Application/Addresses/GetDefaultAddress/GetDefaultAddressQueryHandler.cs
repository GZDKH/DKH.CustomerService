using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Application.Addresses.GetDefaultAddress;

public class GetDefaultAddressQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<GetDefaultAddressQuery, GetDefaultAddressResponse>
{
    public async Task<GetDefaultAddressResponse> Handle(GetDefaultAddressQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken);

        if (profile is null)
        {
            return new GetDefaultAddressResponse { Found = false };
        }

        var address = await dbContext.CustomerAddresses
            .Where(a => a.CustomerId == profile.Id && a.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

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
