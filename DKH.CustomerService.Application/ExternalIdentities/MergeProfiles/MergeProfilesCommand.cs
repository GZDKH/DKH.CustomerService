using DKH.CustomerService.Contracts.Customer.Models.CustomerProfile.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.MergeProfiles;

public sealed record MergeProfilesCommand(
    Guid StorefrontId,
    string SourceUserId,
    string TargetUserId)
    : IRequest<CustomerProfileModel>;
