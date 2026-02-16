using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Services.V1;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using Grpc.Core;

namespace DKH.CustomerService.Application.Profiles.CreateCustomer;

public class CreateCustomerCommandHandler(ICustomerRepository repository)
    : IRequestHandler<CreateCustomerCommand, CreateCustomerResponse>
{
    public async Task<CreateCustomerResponse> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Check if customer already exists
        var existing = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken);

        if (existing is not null)
        {
            throw new RpcException(new Status(
                StatusCode.AlreadyExists,
                $"Customer with UserId '{request.UserId}' already exists in storefront '{request.StorefrontId}'."));
        }

        // Create new customer profile
        var profile = CustomerProfileEntity.Create(
            request.StorefrontId,
            request.UserId,
            request.FirstName,
            request.LastName,
            request.Username,
            request.PhotoUrl,
            request.LanguageCode,
            request.ProviderType);

        // Update with additional fields (phone, email) if provided
        profile.Update(
            firstName: null, // Already set in Create
            lastName: null,  // Already set in Create
            phone: request.Phone,
            email: request.Email,
            languageCode: null); // Already set in Create

        await repository.AddAsync(profile, cancellationToken);

        return new CreateCustomerResponse
        {
            Profile = profile.ToContractModel()
        };
    }
}
