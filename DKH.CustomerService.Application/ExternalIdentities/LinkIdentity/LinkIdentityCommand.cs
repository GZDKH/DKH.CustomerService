using DKH.CustomerService.Contracts.Customer.Models.ExternalIdentity.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.LinkIdentity;

public sealed record LinkIdentityCommand(
    Guid StorefrontId,
    string UserId,
    string Provider,
    string ProviderUserId,
    string? Email,
    string? DisplayName,
    bool IsPrimary)
    : IRequest<ExternalIdentityModel>;
