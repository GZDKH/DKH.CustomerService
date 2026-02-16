using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Admin.BlockCustomer;

public sealed record BlockCustomerCommand(
    Guid StorefrontId,
    string UserId,
    string Reason,
    string BlockedBy)
    : IRequest<BlockCustomerResponse>;
