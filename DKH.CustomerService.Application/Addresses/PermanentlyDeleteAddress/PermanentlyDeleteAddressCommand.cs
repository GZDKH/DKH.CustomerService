namespace DKH.CustomerService.Application.Addresses.PermanentlyDeleteAddress;

public sealed record PermanentlyDeleteAddressCommand(Guid AddressId) : IRequest;

public class PermanentlyDeleteAddressCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<PermanentlyDeleteAddressCommand>
{
    public async Task Handle(PermanentlyDeleteAddressCommand request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.CustomerAddresses
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Id == request.AddressId && e.IsDeleted, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException(
                $"Soft-deleted customer address '{request.AddressId}' not found.");
        }

        await dbContext.CustomerAddresses
            .IgnoreQueryFilters()
            .Where(e => e.Id == request.AddressId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
