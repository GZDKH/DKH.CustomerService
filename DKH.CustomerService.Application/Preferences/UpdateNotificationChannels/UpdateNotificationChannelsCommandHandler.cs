using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Preferences.UpdateNotificationChannels;

public class UpdateNotificationChannelsCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UpdateNotificationChannelsCommand, UpdateNotificationChannelsResponse>
{
    public async Task<UpdateNotificationChannelsResponse> Handle(UpdateNotificationChannelsCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        profile.Preferences.UpdateNotificationChannels(
            request.EmailNotificationsEnabled,
            request.TelegramNotificationsEnabled,
            request.SmsNotificationsEnabled);

        await repository.UpdateAsync(profile, cancellationToken);

        return new UpdateNotificationChannelsResponse
        {
            Preferences = profile.Preferences.ToContractModel(profile.Id),
        };
    }
}
