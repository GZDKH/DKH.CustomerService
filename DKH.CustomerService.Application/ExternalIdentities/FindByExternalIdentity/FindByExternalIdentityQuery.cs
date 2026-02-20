using DKH.CustomerService.Contracts.Customer.Models.CustomerProfile.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.FindByExternalIdentity;

public sealed record FindByExternalIdentityQuery(
    Guid StorefrontId,
    string Provider,
    string ProviderUserId)
    : IRequest<CustomerProfileModel>;
