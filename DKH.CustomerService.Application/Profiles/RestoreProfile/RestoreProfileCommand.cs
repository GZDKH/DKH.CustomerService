using DKH.CustomerService.Domain.Entities.CustomerProfile;

namespace DKH.CustomerService.Application.Profiles.RestoreProfile;

public sealed record RestoreProfileCommand(Guid ProfileId) : IRequest<CustomerProfileEntity>;

public class RestoreProfileCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<RestoreProfileCommand, CustomerProfileEntity>
{
    public async Task<CustomerProfileEntity> Handle(RestoreProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.CustomerProfiles
                          .IgnoreQueryFilters()
                          .SingleOrDefaultAsync(e => e.Id == request.ProfileId && e.IsDeleted, cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Soft-deleted customer profile '{request.ProfileId}' not found.");

        profile.Restore();

        await dbContext.SaveChangesAsync(cancellationToken);

        return profile;
    }
}
