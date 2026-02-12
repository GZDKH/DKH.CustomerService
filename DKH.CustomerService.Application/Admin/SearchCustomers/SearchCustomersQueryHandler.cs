using DKH.CustomerService.Application.Common;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Admin.SearchCustomers;

public class SearchCustomersQueryHandler(ICustomerRepository repository)
    : IRequestHandler<SearchCustomersQuery, SearchCustomersResponse>
{
    public async Task<SearchCustomersResponse> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, totalCount) = await repository.SearchAsync(
            request.StorefrontId,
            request.Query,
            page,
            pageSize,
            cancellationToken);

        var response = new SearchCustomersResponse
        {
            Pagination = PaginationHelper.CreateMetadata(totalCount, page, pageSize),
        };

        response.Customers.AddRange(items.Select(c => c.ToContractModel()));

        return response;
    }
}
