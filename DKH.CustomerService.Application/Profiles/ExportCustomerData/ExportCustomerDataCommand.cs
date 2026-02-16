using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;

namespace DKH.CustomerService.Application.Profiles.ExportCustomerData;

public sealed record ExportCustomerDataCommand(
    Guid StorefrontId,
    string UserId,
    string Format)
    : IRequest<ExportCustomerDataResponse>;
