using DKH.CustomerService.Application.Common;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;

namespace DKH.CustomerService.Application.Admin.ListCustomers;

public class ListCustomersQueryHandler(ICustomerRepository repository)
    : IRequestHandler<ListCustomersQuery, ListCustomersResponse>
{
    public async Task<ListCustomersResponse> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

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
