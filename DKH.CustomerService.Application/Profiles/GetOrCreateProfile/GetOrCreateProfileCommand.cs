using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Profiles.GetOrCreateProfile;

public sealed record GetOrCreateProfileCommand(
    Guid StorefrontId,
    string UserId,
    string FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl,
    string? LanguageCode)
    : IRequest<GetOrCreateProfileResponse>;
