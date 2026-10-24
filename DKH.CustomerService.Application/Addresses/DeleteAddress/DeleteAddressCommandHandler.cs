using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;

namespace DKH.CustomerService.Application.Addresses.DeleteAddress;

public class DeleteAddressCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<DeleteAddressCommand, DeleteAddressResponse>
{
    public async Task<DeleteAddressResponse> Handle(DeleteAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await CustomerAddressOwnership.OwnedAddresses(dbContext, request.StorefrontId, request.UserId)
            .SingleOrDefaultAsync(value => value.Id == request.AddressId && !value.IsDeleted, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (address is null)
        {
            return new DeleteAddressResponse { Success = false };
        }

        dbContext.CustomerAddresses.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return new DeleteAddressResponse { Success = true };
    }
}
