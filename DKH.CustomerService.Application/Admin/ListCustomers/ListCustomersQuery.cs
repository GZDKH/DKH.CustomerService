using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;
using DKH.Platform.Domain.Enums;

namespace DKH.CustomerService.Application.Admin.ListCustomers;

public sealed record ListCustomersQuery(
    Guid? StorefrontId,  // Nullable for admin - returns all customers when null
    int Page,
    int PageSize,
    string? SortBy,
    bool SortDescending,
    PlatformSoftDeleteFilter SoftDeleteFilter = PlatformSoftDeleteFilter.ActiveOnly)
    : IRequest<ListCustomersResponse>;
