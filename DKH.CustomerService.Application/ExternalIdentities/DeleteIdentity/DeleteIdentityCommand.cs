namespace DKH.CustomerService.Application.ExternalIdentities.DeleteIdentity;

public sealed record DeleteIdentityCommand(Guid IdentityId) : IRequest;

public class DeleteIdentityCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<DeleteIdentityCommand>
{
    public async Task Handle(DeleteIdentityCommand request, CancellationToken cancellationToken)
    {
        var identity = await dbContext.ExternalIdentities
                           .FirstOrDefaultAsync(e => e.Id == request.IdentityId, cancellationToken)
                       ?? throw new InvalidOperationException(
                           $"External identity '{request.IdentityId}' not found.");

        identity.MarkAsDeleted();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
