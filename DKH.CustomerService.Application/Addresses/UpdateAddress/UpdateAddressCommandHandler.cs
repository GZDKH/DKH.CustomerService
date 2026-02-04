using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using Grpc.Core;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.UpdateAddress;

public class UpdateAddressCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<UpdateAddressCommand, UpdateAddressResponse>
{
    public async Task<UpdateAddressResponse> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await dbContext.CustomerAddresses.FindAsync([request.AddressId], cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Address not found"));

        address.Update(
            request.Label,
            request.Country,
            request.City,
            request.Street,
            request.Building,
            request.Apartment,
            request.PostalCode,
            request.Phone);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateAddressResponse
        {
            Address = address.ToContractModel(),
        };
    }
}
