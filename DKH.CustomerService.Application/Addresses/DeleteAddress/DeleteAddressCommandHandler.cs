using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.DeleteAddress;

public class DeleteAddressCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<DeleteAddressCommand, DeleteAddressResponse>
{
    public async Task<DeleteAddressResponse> Handle(DeleteAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await dbContext.CustomerAddresses.FindAsync([request.AddressId], cancellationToken);

        if (address is null)
        {
            return new DeleteAddressResponse { Success = false };
        }

        dbContext.CustomerAddresses.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteAddressResponse { Success = true };
    }
}
