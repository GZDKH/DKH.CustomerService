using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Admin.SearchCustomers;

public sealed record SearchCustomersQuery(
    Guid StorefrontId,
    string Query,
    int Page,
    int PageSize)
    : IRequest<SearchCustomersResponse>;
