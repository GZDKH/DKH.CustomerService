using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using DKH.Platform.Domain.Enums;
using DKH.Platform.EntityFrameworkCore;

namespace DKH.CustomerService.Application.Addresses.ListAddresses;

public class ListAddressesQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<ListAddressesQuery, ListAddressesResponse>
{
    public async Task<ListAddressesResponse> Handle(ListAddressesQuery request, CancellationToken cancellationToken)
    {
        var response = new ListAddressesResponse();

        var query = CustomerAddressOwnership.OwnedAddresses(dbContext, request.StorefrontId, request.UserId)
            .AsNoTracking()
            .ApplySoftDeleteFilter(request.SoftDeleteFilter);
        // Platform ActiveOnly preserves the normal query; this Address model has
        // no automatic filter, so its ordinary active predicate must be explicit.
        if (request.SoftDeleteFilter == PlatformSoftDeleteFilter.ActiveOnly)
        {
            query = query.Where(address => !address.IsDeleted);
        }

        var addresses = await query.ToListAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        response.Addresses.AddRange(addresses.Select(address => address.ToContractModel()));

        return response;
    }
}
