using DKH.CustomerService.Contracts.Customer.Api.IdentityLinking.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.ListIdentities;

public sealed record ListIdentitiesQuery(
    Guid StorefrontId,
    string UserId)
    : IRequest<ListIdentitiesResponse>;
