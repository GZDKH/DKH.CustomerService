using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;

namespace DKH.CustomerService.Application.Admin.SearchCustomers;

public sealed record SearchCustomersQuery(
    Guid? StorefrontId,  // Nullable for admin - returns all customers when null
    string Query,
    int Page,
    int PageSize)
    : IRequest<SearchCustomersResponse>;
