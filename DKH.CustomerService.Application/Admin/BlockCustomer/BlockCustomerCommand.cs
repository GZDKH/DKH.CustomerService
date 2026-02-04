using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Admin.BlockCustomer;

public sealed record BlockCustomerCommand(
    Guid StorefrontId,
    string TelegramUserId,
    string Reason,
    string BlockedBy)
    : IRequest<BlockCustomerResponse>;
