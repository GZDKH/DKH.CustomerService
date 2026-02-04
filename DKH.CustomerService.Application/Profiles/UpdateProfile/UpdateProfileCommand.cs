using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Profiles.UpdateProfile;

public sealed record UpdateProfileCommand(
    Guid StorefrontId,
    string TelegramUserId,
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    string? LanguageCode)
    : IRequest<UpdateProfileResponse>;
