using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using DKH.Platform.Domain.Enums;
using DKH.Platform.EntityFrameworkCore;

namespace DKH.CustomerService.Application.Addresses.ListAddresses;

public class ListAddressesQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<ListAddressesQuery, ListAddressesResponse>
{
    public async Task<ListAddressesResponse> Handle(ListAddressesQuery request, CancellationToken cancellationToken)
    {
        var response = new ListAddressesResponse();

        if (request.SoftDeleteFilter != PlatformSoftDeleteFilter.ActiveOnly)
        {
            var profile = await dbContext.CustomerProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.StorefrontId == request.StorefrontId && p.UserId == request.UserId,
                    cancellationToken);

            if (profile is not null)
            {
                var addresses = await dbContext.CustomerAddresses
                    .AsNoTracking()
                    .ApplySoftDeleteFilter(request.SoftDeleteFilter)
                    .Where(a => a.CustomerId == profile.Id)
                    .ToListAsync(cancellationToken);

                response.Addresses.AddRange(addresses.Select(a => a.ToContractModel()));
            }
        }
        else
        {
            var profile = await repository.GetByUserIdWithAddressesAsync(
                request.StorefrontId,
                request.UserId,
                cancellationToken);

            if (profile is not null)
            {
                response.Addresses.AddRange(profile.Addresses.Select(a => a.ToContractModel()));
            }
        }

        return response;
    }
}
