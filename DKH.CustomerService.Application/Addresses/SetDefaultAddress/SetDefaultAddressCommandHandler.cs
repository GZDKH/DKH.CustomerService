using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Addresses.SetDefaultAddress;

public class SetDefaultAddressCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<SetDefaultAddressCommand, SetDefaultAddressResponse>
{
    public async Task<SetDefaultAddressResponse> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        var profile = await CustomerAddressOwnership.ActiveProfiles(dbContext, request.StorefrontId, request.UserId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        var address = await CustomerAddressOwnership.OwnedAddresses(dbContext, request.StorefrontId, request.UserId)
            .SingleOrDefaultAsync(value => value.Id == request.AddressId && !value.IsDeleted, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (address is null || address.CustomerId != profile.Id)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Address not found"));
        }

        var existingDefault = await dbContext.CustomerAddresses
            .Where(a => a.CustomerId == profile.Id && !a.IsDeleted && a.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        existingDefault?.SetDefault(false);

        address.SetDefault(true);
        await dbContext.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return new SetDefaultAddressResponse { Success = true };
    }
}
