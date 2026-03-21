using DKH.CustomerService.Domain.Entities.ExternalIdentity;

namespace DKH.CustomerService.Application.ExternalIdentities.RestoreIdentity;

public sealed record RestoreIdentityCommand(Guid IdentityId) : IRequest<CustomerExternalIdentityEntity>;

public class RestoreIdentityCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<RestoreIdentityCommand, CustomerExternalIdentityEntity>
{
    public async Task<CustomerExternalIdentityEntity> Handle(RestoreIdentityCommand request, CancellationToken cancellationToken)
    {
        var identity = await dbContext.ExternalIdentities
                           .IgnoreQueryFilters()
                           .SingleOrDefaultAsync(e => e.Id == request.IdentityId && e.IsDeleted, cancellationToken)
                       ?? throw new InvalidOperationException(
                           $"Soft-deleted external identity '{request.IdentityId}' not found.");

        identity.Restore();

        await dbContext.SaveChangesAsync(cancellationToken);

        return identity;
    }
}
