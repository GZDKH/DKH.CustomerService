namespace DKH.CustomerService.Application.ExternalIdentities.UnlinkIdentity;

public sealed record UnlinkIdentityCommand(
    Guid StorefrontId,
    string UserId,
    Guid IdentityId)
    : IRequest;
