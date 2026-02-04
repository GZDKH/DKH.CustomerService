using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using Grpc.Core;
using MediatR;

namespace DKH.CustomerService.Application.Profiles.UpdateProfile;

public class UpdateProfileCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UpdateProfileCommand, UpdateProfileResponse>
{
    public async Task<UpdateProfileResponse> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        profile.Update(
            request.FirstName,
            request.LastName,
            request.Phone,
            request.Email,
            request.LanguageCode);

        await repository.UpdateAsync(profile, cancellationToken);

        return new UpdateProfileResponse
        {
            Profile = profile.ToContractModel(),
        };
    }
}
