using DKH.CustomerService.Contracts.Api.V1;

namespace DKH.CustomerService.Application.Profiles.DeleteProfile;

public class DeleteProfileCommandHandler(ICustomerRepository repository)
    : IRequestHandler<DeleteProfileCommand, DeleteProfileResponse>
{
    public async Task<DeleteProfileResponse> Handle(DeleteProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken);

        if (profile is null)
        {
            return new DeleteProfileResponse { Success = false };
        }

        if (request.HardDelete)
        {
            await repository.DeleteAsync(profile, cancellationToken);
        }
        else
        {
            profile.SoftDelete();
            await repository.UpdateAsync(profile, cancellationToken);
        }

        return new DeleteProfileResponse { Success = true };
    }
}
