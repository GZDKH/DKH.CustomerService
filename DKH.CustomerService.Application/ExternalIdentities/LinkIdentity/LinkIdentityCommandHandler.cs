using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Models.ExternalIdentity.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.LinkIdentity;

public class LinkIdentityCommandHandler(ICustomerRepository repository)
    : IRequestHandler<LinkIdentityCommand, ExternalIdentityModel>
{
    public async Task<ExternalIdentityModel> Handle(LinkIdentityCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdWithExternalIdentitiesAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Customer profile not found for user '{request.UserId}' in storefront '{request.StorefrontId}'.");

        var identity = profile.AddExternalIdentity(
            request.Provider,
            request.ProviderUserId,
            request.Email,
            request.DisplayName,
            request.IsPrimary);

        await repository.UpdateAsync(profile, cancellationToken);

        return identity.ToContractModel();
    }
}
