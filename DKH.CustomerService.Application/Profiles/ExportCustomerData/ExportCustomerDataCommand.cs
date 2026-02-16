using DKH.CustomerService.Contracts.Services.V1;

namespace DKH.CustomerService.Application.Profiles.ExportCustomerData;

public sealed record ExportCustomerDataCommand(
    Guid StorefrontId,
    string UserId,
    string Format)
    : IRequest<ExportCustomerDataResponse>;
