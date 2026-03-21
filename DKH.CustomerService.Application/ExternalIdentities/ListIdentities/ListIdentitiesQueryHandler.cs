using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.IdentityLinking.v1;
using DKH.Platform.Domain.Enums;
using DKH.Platform.EntityFrameworkCore;

namespace DKH.CustomerService.Application.ExternalIdentities.ListIdentities;

public class ListIdentitiesQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<ListIdentitiesQuery, ListIdentitiesResponse>
{
    public async Task<ListIdentitiesResponse> Handle(ListIdentitiesQuery request, CancellationToken cancellationToken)
    {
        var response = new ListIdentitiesResponse();

        if (request.SoftDeleteFilter != PlatformSoftDeleteFilter.ActiveOnly)
        {
            var profile = await dbContext.CustomerProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.StorefrontId == request.StorefrontId && p.UserId == request.UserId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Customer profile not found for user '{request.UserId}' in storefront '{request.StorefrontId}'.");

            var identities = await dbContext.ExternalIdentities
                .AsNoTracking()
                .ApplySoftDeleteFilter(request.SoftDeleteFilter)
                .Where(e => e.CustomerId == profile.Id)
                .ToListAsync(cancellationToken);

            response.Items.AddRange(identities.Select(e => e.ToContractModel()));
        }
        else
        {
            var profile = await repository.GetByUserIdWithExternalIdentitiesAsync(
                request.StorefrontId,
                request.UserId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Customer profile not found for user '{request.UserId}' in storefront '{request.StorefrontId}'.");

            response.Items.AddRange(profile.ExternalIdentities.Select(e => e.ToContractModel()));
        }

        return response;
    }
}
