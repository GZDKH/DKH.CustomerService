using System.Diagnostics;
using DKH.CustomerService.Application.Observability;

namespace DKH.CustomerService.Application.ExternalIdentities.UnlinkIdentity;

public class UnlinkIdentityCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UnlinkIdentityCommand>
{
    public async Task Handle(UnlinkIdentityCommand request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var profile = await repository.GetByUserIdWithExternalIdentitiesAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Customer profile not found for user '{request.UserId}' in storefront '{request.StorefrontId}'.");
        var provider = profile.ExternalIdentities
            .FirstOrDefault(identity => identity.Id == request.IdentityId && !identity.IsDeleted)
            ?.Provider;

        profile.RemoveExternalIdentity(request.IdentityId);

        await repository.UpdateAsync(profile, cancellationToken);
        CustomerAccountMetrics.RecordIdentity(
            CustomerIdentityOutcome.Unlinked,
            provider,
            request.StorefrontId,
            Stopwatch.GetElapsedTime(startedAt));
    }
}
