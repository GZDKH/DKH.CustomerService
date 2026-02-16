using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerPreferencesManagement.v1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Preferences.UpdateNotificationTypes;

public class UpdateNotificationTypesCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UpdateNotificationTypesCommand, UpdateNotificationTypesResponse>
{
    public async Task<UpdateNotificationTypesResponse> Handle(UpdateNotificationTypesCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        profile.Preferences.UpdateNotificationTypes(
            request.OrderStatusUpdates,
            request.PromotionalOffers);

        await repository.UpdateAsync(profile, cancellationToken);

        return new UpdateNotificationTypesResponse
        {
            Preferences = profile.Preferences.ToContractModel(profile.Id),
        };
    }
}
