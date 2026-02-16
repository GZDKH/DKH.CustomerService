using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Profiles.DeleteProfile;

public sealed record DeleteProfileCommand(
    Guid StorefrontId,
    string UserId,
    bool HardDelete)
    : IRequest<DeleteProfileResponse>;
