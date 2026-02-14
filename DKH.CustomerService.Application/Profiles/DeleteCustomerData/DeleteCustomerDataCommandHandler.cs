using DKH.CustomerService.Contracts.Services.V1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Profiles.DeleteCustomerData;

public class DeleteCustomerDataCommandHandler(ICustomerRepository repository)
    : IRequestHandler<DeleteCustomerDataCommand, DeleteCustomerDataResponse>
{
    public async Task<DeleteCustomerDataResponse> Handle(DeleteCustomerDataCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        if (request.Anonymize)
        {
            // GDPR compliance: Anonymize customer data
            profile.Anonymize();
            await repository.UpdateAsync(profile, cancellationToken);

            return new DeleteCustomerDataResponse
            {
                Success = true,
                AnonymizedId = profile.Id.ToString()
            };
        }

        // Soft delete
        profile.SoftDelete();
        await repository.UpdateAsync(profile, cancellationToken);

        return new DeleteCustomerDataResponse
        {
            Success = true
        };
    }
}
