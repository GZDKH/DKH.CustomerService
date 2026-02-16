using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;

namespace DKH.CustomerService.Application.Admin.BlockCustomer;

public sealed record BlockCustomerCommand(
    Guid StorefrontId,
    string UserId,
    string Reason,
    string BlockedBy)
    : IRequest<BlockCustomerResponse>;
