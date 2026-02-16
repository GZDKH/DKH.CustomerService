using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;

namespace DKH.CustomerService.Application.Profiles.GetProfile;

public sealed record GetProfileQuery(Guid StorefrontId, string UserId)
    : IRequest<GetProfileResponse>;
