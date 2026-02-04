using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Profiles.DeleteProfile;

public sealed record DeleteProfileCommand(
    Guid StorefrontId,
    string TelegramUserId,
    bool HardDelete)
    : IRequest<DeleteProfileResponse>;
