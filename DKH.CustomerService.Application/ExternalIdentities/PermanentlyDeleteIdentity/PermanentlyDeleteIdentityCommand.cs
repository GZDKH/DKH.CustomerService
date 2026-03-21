namespace DKH.CustomerService.Application.ExternalIdentities.PermanentlyDeleteIdentity;

public sealed record PermanentlyDeleteIdentityCommand(Guid IdentityId) : IRequest;

public class PermanentlyDeleteIdentityCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<PermanentlyDeleteIdentityCommand>
{
    public async Task Handle(PermanentlyDeleteIdentityCommand request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.ExternalIdentities
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Id == request.IdentityId && e.IsDeleted, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException(
                $"Soft-deleted external identity '{request.IdentityId}' not found.");
        }

        await dbContext.ExternalIdentities
            .IgnoreQueryFilters()
            .Where(e => e.Id == request.IdentityId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
