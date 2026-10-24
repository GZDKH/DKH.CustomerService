using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Addresses.UpdateAddress;

public class UpdateAddressCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<UpdateAddressCommand, UpdateAddressResponse>
{
    public async Task<UpdateAddressResponse> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await CustomerAddressOwnership.OwnedAddresses(dbContext, request.StorefrontId, request.UserId)
            .SingleOrDefaultAsync(value => value.Id == request.AddressId && !value.IsDeleted, cancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Address not found"));
        cancellationToken.ThrowIfCancellationRequested();

        address.Update(
            request.Label,
            request.Country,
            request.City,
            request.Street,
            request.Building,
            request.Apartment,
            request.PostalCode,
            request.Phone,
            request.FirstName,
            request.LastName,
            request.Province,
            request.Company);

        await dbContext.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return new UpdateAddressResponse
        {
            Address = address.ToContractModel(),
        };
    }
}
