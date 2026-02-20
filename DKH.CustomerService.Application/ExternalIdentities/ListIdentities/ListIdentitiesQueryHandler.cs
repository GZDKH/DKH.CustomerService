using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.IdentityLinking.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.ListIdentities;

public class ListIdentitiesQueryHandler(ICustomerRepository repository)
    : IRequestHandler<ListIdentitiesQuery, ListIdentitiesResponse>
{
    public async Task<ListIdentitiesResponse> Handle(ListIdentitiesQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdWithExternalIdentitiesAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Customer profile not found for user '{request.UserId}' in storefront '{request.StorefrontId}'.");

        var response = new ListIdentitiesResponse();
        response.Items.AddRange(profile.ExternalIdentities.Select(e => e.ToContractModel()));
        return response;
    }
}
