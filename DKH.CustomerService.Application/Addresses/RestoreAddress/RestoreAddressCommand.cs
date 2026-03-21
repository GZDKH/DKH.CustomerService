using DKH.CustomerService.Domain.Entities.CustomerAddress;

namespace DKH.CustomerService.Application.Addresses.RestoreAddress;

public sealed record RestoreAddressCommand(Guid AddressId) : IRequest<CustomerAddressEntity>;

public class RestoreAddressCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<RestoreAddressCommand, CustomerAddressEntity>
{
    public async Task<CustomerAddressEntity> Handle(RestoreAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await dbContext.CustomerAddresses
                          .IgnoreQueryFilters()
                          .SingleOrDefaultAsync(e => e.Id == request.AddressId && e.IsDeleted, cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Soft-deleted customer address '{request.AddressId}' not found.");

        address.Restore();

        await dbContext.SaveChangesAsync(cancellationToken);

        return address;
    }
}
