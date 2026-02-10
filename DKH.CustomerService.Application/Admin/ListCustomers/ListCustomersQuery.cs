using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Admin.ListCustomers;

public sealed record ListCustomersQuery(
    Guid? StorefrontId,  // Nullable for admin - returns all customers when null
    int Page,
    int PageSize,
    string? SortBy,
    bool SortDescending)
    : IRequest<ListCustomersResponse>;
