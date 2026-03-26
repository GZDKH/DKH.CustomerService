using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;

namespace DKH.CustomerService.Application.Admin.GetCustomerStats;

public sealed record GetCustomerStatsQuery(
    Guid? StorefrontId,
    string UserId)
    : IRequest<GetCustomerStatsResponse>;
