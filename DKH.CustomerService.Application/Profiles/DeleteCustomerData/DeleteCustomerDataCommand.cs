using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;

namespace DKH.CustomerService.Application.Profiles.DeleteCustomerData;

public sealed record DeleteCustomerDataCommand(
    Guid StorefrontId,
    string UserId,
    bool Anonymize)
    : IRequest<DeleteCustomerDataResponse>;
