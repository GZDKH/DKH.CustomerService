using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Models.CustomerProfile.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.FindByExternalIdentity;

public class FindByExternalIdentityQueryHandler(ICustomerRepository repository)
    : IRequestHandler<FindByExternalIdentityQuery, CustomerProfileModel>
{
    public async Task<CustomerProfileModel> Handle(FindByExternalIdentityQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByExternalIdentityAsync(
            request.StorefrontId,
            request.Provider,
            request.ProviderUserId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Customer profile not found for provider '{request.Provider}' with user ID '{request.ProviderUserId}'.");

        return profile.ToContractModel();
    }
}
