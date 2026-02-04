using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Profiles.GetOrCreateProfile;

public sealed record GetOrCreateProfileCommand(
    Guid StorefrontId,
    string TelegramUserId,
    string FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl,
    string? LanguageCode)
    : IRequest<GetOrCreateProfileResponse>;
