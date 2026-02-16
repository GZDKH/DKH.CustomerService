using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerManagement.v1;
using DKH.CustomerService.Domain.Entities.CustomerProfile;

namespace DKH.CustomerService.Application.Profiles.GetOrCreateProfile;

public class GetOrCreateProfileCommandHandler(ICustomerRepository repository)
    : IRequestHandler<GetOrCreateProfileCommand, GetOrCreateProfileResponse>
{
    public async Task<GetOrCreateProfileResponse> Handle(GetOrCreateProfileCommand request, CancellationToken cancellationToken)
    {
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

        var profile = CustomerProfileEntity.Create(
            request.StorefrontId,
            request.UserId,
            request.FirstName,
            request.LastName,
            request.Username,
            request.PhotoUrl,
            languageCode: request.LanguageCode);

        await repository.AddAsync(profile, cancellationToken);

        return new GetOrCreateProfileResponse
        {
            Profile = profile.ToContractModel(),
            Created = true,
        };
    }
}
