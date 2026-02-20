namespace DKH.CustomerService.Application.ExternalIdentities.UnlinkIdentity;

public class UnlinkIdentityCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UnlinkIdentityCommand>
{
    public async Task Handle(UnlinkIdentityCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdWithExternalIdentitiesAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Customer profile not found for user '{request.UserId}' in storefront '{request.StorefrontId}'.");

        profile.RemoveExternalIdentity(request.IdentityId);

        await repository.UpdateAsync(profile, cancellationToken);
    }
}
