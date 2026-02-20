using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Models.CustomerProfile.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.MergeProfiles;

public class MergeProfilesCommandHandler(ICustomerRepository repository)
    : IRequestHandler<MergeProfilesCommand, CustomerProfileModel>
{
    public async Task<CustomerProfileModel> Handle(
        MergeProfilesCommand request,
        CancellationToken cancellationToken)
    {
        var sourceProfile = await repository.GetByUserIdWithAllRelationsAsync(
            request.StorefrontId, request.SourceUserId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Source profile with userId '{request.SourceUserId}' not found in storefront '{request.StorefrontId}'.");

        var targetProfile = await repository.GetByUserIdWithAllRelationsAsync(
            request.StorefrontId, request.TargetUserId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Target profile with userId '{request.TargetUserId}' not found in storefront '{request.StorefrontId}'.");

        targetProfile.MergeFrom(sourceProfile);
        sourceProfile.SoftDelete();

        await repository.UpdateAsync(targetProfile, cancellationToken);
        await repository.UpdateAsync(sourceProfile, cancellationToken);

        return targetProfile.ToContractModel();
    }
}
