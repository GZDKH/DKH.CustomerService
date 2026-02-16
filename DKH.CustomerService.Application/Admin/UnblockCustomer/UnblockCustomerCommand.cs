using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;

namespace DKH.CustomerService.Application.Admin.UnblockCustomer;

public sealed record UnblockCustomerCommand(Guid StorefrontId, string UserId)
    : IRequest<UnblockCustomerResponse>;
