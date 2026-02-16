using DKH.CustomerService.Contracts.Services.V1;

namespace DKH.CustomerService.Application.Profiles.DeleteCustomerData;

public sealed record DeleteCustomerDataCommand(
    Guid StorefrontId,
    string UserId,
    bool Anonymize)
    : IRequest<DeleteCustomerDataResponse>;
