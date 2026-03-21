using DKH.CustomerService.Contracts.Customer.Api.IdentityLinking.v1;
using DKH.Platform.Domain.Enums;

namespace DKH.CustomerService.Application.ExternalIdentities.ListIdentities;

public sealed record ListIdentitiesQuery(
    Guid StorefrontId,
    string UserId,
    PlatformSoftDeleteFilter SoftDeleteFilter = PlatformSoftDeleteFilter.ActiveOnly)
    : IRequest<ListIdentitiesResponse>;
