namespace DKH.CustomerService.Application.Profiles.PermanentlyDeleteProfile;

public sealed record PermanentlyDeleteProfileCommand(Guid ProfileId) : IRequest;

public class PermanentlyDeleteProfileCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<PermanentlyDeleteProfileCommand>
{
    public async Task Handle(PermanentlyDeleteProfileCommand request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.CustomerProfiles
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Id == request.ProfileId && e.IsDeleted, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException(
                $"Soft-deleted customer profile '{request.ProfileId}' not found.");
        }

        // Delete children first
        await dbContext.ExternalIdentities
            .IgnoreQueryFilters()
            .Where(e => e.CustomerId == request.ProfileId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.CustomerAddresses
            .IgnoreQueryFilters()
            .Where(a => a.CustomerId == request.ProfileId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.WishlistItems
            .IgnoreQueryFilters()
            .Where(w => w.CustomerId == request.ProfileId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.CustomerProfiles
            .IgnoreQueryFilters()
            .Where(e => e.Id == request.ProfileId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
