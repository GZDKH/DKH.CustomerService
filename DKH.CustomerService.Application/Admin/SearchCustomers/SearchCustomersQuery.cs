using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Admin.SearchCustomers;

public sealed record SearchCustomersQuery(
    Guid? StorefrontId,  // Nullable for admin - returns all customers when null
    string Query,
    int Page,
    int PageSize)
    : IRequest<SearchCustomersResponse>;
