using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Admin.ListCustomers;

public sealed record ListCustomersQuery(
    Guid StorefrontId,
    int Page,
    int PageSize,
    string? SortBy,
    bool SortDescending)
    : IRequest<ListCustomersResponse>;
