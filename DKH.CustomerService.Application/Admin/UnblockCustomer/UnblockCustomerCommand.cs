using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Admin.UnblockCustomer;

public sealed record UnblockCustomerCommand(Guid StorefrontId, string TelegramUserId)
    : IRequest<UnblockCustomerResponse>;
