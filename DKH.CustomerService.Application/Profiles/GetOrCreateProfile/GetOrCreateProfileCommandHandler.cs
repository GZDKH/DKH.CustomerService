using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;
using DKH.CustomerService.Domain.Entities.CustomerProfile;

namespace DKH.CustomerService.Application.Profiles.GetOrCreateProfile;

public class GetOrCreateProfileCommandHandler(ICustomerRepository repository)
    : IRequestHandler<GetOrCreateProfileCommand, GetOrCreateProfileResponse>
{
    public async Task<GetOrCreateProfileResponse> Handle(GetOrCreateProfileCommand request, CancellationToken cancellationToken)
    {
        // 1. Primary lookup: by Keycloak user ID
        var existing = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken);

        if (existing is not null)
        {
            existing.UpdateFromTelegram(
                request.FirstName,
                request.LastName,
                request.Username,
                request.PhotoUrl,
                request.LanguageCode);
            await repository.UpdateAsync(existing, cancellationToken);

            return new GetOrCreateProfileResponse
            {
                Profile = existing.ToContractModel(),
                Created = false,
            };
        }

        // 2. Fallback: lookup by provider identity (e.g., Telegram ID, Google ID)
        if (!string.IsNullOrWhiteSpace(request.Provider) && !string.IsNullOrWhiteSpace(request.ProviderUserId))
        {
            existing = await repository.GetByExternalIdentityAsync(
                request.StorefrontId,
                request.Provider,
                request.ProviderUserId,
                cancellationToken);

            if (existing is not null)
            {
                existing.UpdateUserId(request.UserId);
                existing.UpdateFromTelegram(
                    request.FirstName,
                    request.LastName,
                    request.Username,
                    request.PhotoUrl,
                    request.LanguageCode);
                await repository.UpdateAsync(existing, cancellationToken);

                return new GetOrCreateProfileResponse
                {
                    Profile = existing.ToContractModel(),
                    Created = false,
                };
            }
        }

        // 3. Create new profile
        var profile = CustomerProfileEntity.Create(
            request.StorefrontId,
            request.UserId,
            request.FirstName,
            request.LastName,
            request.Username,
            request.PhotoUrl,
            languageCode: request.LanguageCode);

        // Auto-link provider identity on creation
        if (!string.IsNullOrWhiteSpace(request.Provider) && !string.IsNullOrWhiteSpace(request.ProviderUserId))
        {
            profile.AddExternalIdentity(
                request.Provider,
                request.ProviderUserId,
                isPrimary: true);
        }

        await repository.AddAsync(profile, cancellationToken);

        return new GetOrCreateProfileResponse
        {
            Profile = profile.ToContractModel(),
            Created = true,
        };
    }
}
