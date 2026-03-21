using DKH.CustomerService.Application.Common;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;
using DKH.Platform.Domain.Enums;
using DKH.Platform.EntityFrameworkCore;

namespace DKH.CustomerService.Application.Admin.ListCustomers;

public class ListCustomersQueryHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<ListCustomersQuery, ListCustomersResponse>
{
    public async Task<ListCustomersResponse> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        if (request.SoftDeleteFilter != PlatformSoftDeleteFilter.ActiveOnly)
        {
            // Use direct dbContext query with soft-delete filter bypass
            var baseQuery = dbContext.CustomerProfiles
                .AsNoTracking()
                .ApplySoftDeleteFilter(request.SoftDeleteFilter);

            if (request.StorefrontId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.StorefrontId == request.StorefrontId.Value);
            }

            var totalCount = await baseQuery.CountAsync(cancellationToken);
            var items = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var response = new ListCustomersResponse
            {
                Pagination = PaginationHelper.CreateMetadata(totalCount, page, pageSize),
            };

            response.Customers.AddRange(items.Select(c => c.ToContractModel()));

            return response;
        }
        else
        {
            var (items, totalCount) = await repository.ListAsync(
                request.StorefrontId,
                page,
                pageSize,
                request.SortBy,
                request.SortDescending,
                cancellationToken);

            var response = new ListCustomersResponse
            {
                Pagination = PaginationHelper.CreateMetadata(totalCount, page, pageSize),
            };

            response.Customers.AddRange(items.Select(c => c.ToContractModel()));

            return response;
        }
    }
}
