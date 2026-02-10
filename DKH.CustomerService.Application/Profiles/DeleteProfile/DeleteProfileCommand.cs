using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Profiles.DeleteProfile;

public sealed record DeleteProfileCommand(
    Guid StorefrontId,
    string TelegramUserId,
    bool HardDelete)
    : IRequest<DeleteProfileResponse>;
