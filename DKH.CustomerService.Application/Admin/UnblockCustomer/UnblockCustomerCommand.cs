using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Admin.UnblockCustomer;

public sealed record UnblockCustomerCommand(Guid StorefrontId, string TelegramUserId)
    : IRequest<UnblockCustomerResponse>;
