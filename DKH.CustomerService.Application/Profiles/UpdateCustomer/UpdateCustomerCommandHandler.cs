using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Services.V1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Profiles.UpdateCustomer;

public class UpdateCustomerCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UpdateCustomerCommand, UpdateCustomerResponse>
{
    public async Task<UpdateCustomerResponse> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        // Update profile with provided fields
        profile.Update(
            firstName: request.FirstName,
            lastName: request.LastName,
            username: request.Username,
            phone: request.Phone,
            email: request.Email,
            languageCode: request.LanguageCode,
            photoUrl: request.PhotoUrl,
            isPremium: request.IsPremium);

        await repository.UpdateAsync(profile, cancellationToken);

        return new UpdateCustomerResponse
        {
            Profile = profile.ToContractModel()
        };
    }
}
