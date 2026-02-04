using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Api.V1;
using Grpc.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Application.Addresses.SetDefaultAddress;

public class SetDefaultAddressCommandHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<SetDefaultAddressCommand, SetDefaultAddressResponse>
{
    public async Task<SetDefaultAddressResponse> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        var address = await dbContext.CustomerAddresses.FindAsync([request.AddressId], cancellationToken);

        if (address is null || address.CustomerId != profile.Id)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Address not found"));
        }

        var existingDefault = await dbContext.CustomerAddresses
            .Where(a => a.CustomerId == profile.Id && a.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        existingDefault?.SetDefault(false);

        address.SetDefault(true);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SetDefaultAddressResponse { Success = true };
    }
}
