using System.Diagnostics;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Application.Observability;
using DKH.CustomerService.Contracts.Customer.Models.ExternalIdentity.v1;

namespace DKH.CustomerService.Application.ExternalIdentities.LinkIdentity;

public class LinkIdentityCommandHandler(ICustomerRepository repository)
    : IRequestHandler<LinkIdentityCommand, ExternalIdentityModel>
{
    public async Task<ExternalIdentityModel> Handle(LinkIdentityCommand request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var profile = await repository.GetByUserIdWithExternalIdentitiesAsync(
                request.StorefrontId,
                request.UserId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Customer profile not found for user '{request.UserId}' in storefront '{request.StorefrontId}'.");

            var identity = profile.AddExternalIdentity(
                request.Provider,
                request.ProviderUserId,
                request.Email,
                request.DisplayName,
                request.IsPrimary);

            await repository.UpdateAsync(profile, cancellationToken);

            CustomerAccountMetrics.RecordIdentity(
                CustomerIdentityOutcome.Linked,
                request.Provider,
                request.StorefrontId,
                Stopwatch.GetElapsedTime(startedAt));
            return identity.ToContractModel();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            CustomerAccountMetrics.RecordIdentity(
                CustomerIdentityOutcome.LinkRejected,
                request.Provider,
                request.StorefrontId,
                Stopwatch.GetElapsedTime(startedAt));
            throw;
        }
    }
}
