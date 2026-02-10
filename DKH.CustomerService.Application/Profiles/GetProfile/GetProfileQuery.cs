using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Profiles.GetProfile;

public sealed record GetProfileQuery(Guid StorefrontId, string TelegramUserId)
    : IRequest<GetProfileResponse>;
