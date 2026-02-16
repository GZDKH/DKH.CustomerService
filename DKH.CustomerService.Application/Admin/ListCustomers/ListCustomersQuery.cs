using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;

namespace DKH.CustomerService.Application.Admin.ListCustomers;

public sealed record ListCustomersQuery(
    Guid? StorefrontId,  // Nullable for admin - returns all customers when null
    int Page,
    int PageSize,
    string? SortBy,
    bool SortDescending)
    : IRequest<ListCustomersResponse>;
