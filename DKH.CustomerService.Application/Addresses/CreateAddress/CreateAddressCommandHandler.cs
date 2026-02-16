using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using Grpc.Core;

namespace DKH.CustomerService.Application.Addresses.CreateAddress;

public class CreateAddressCommandHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<CreateAddressCommand, CreateAddressResponse>
{
    public async Task<CreateAddressResponse> Handle(CreateAddressCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        if (request.IsDefault)
        {
            var existingDefault = await dbContext.CustomerAddresses
                .Where(a => a.CustomerId == profile.Id && a.IsDefault)
                .FirstOrDefaultAsync(cancellationToken);

            existingDefault?.SetDefault(false);
        }

        var address = CustomerAddressEntity.Create(
            profile.Id,
            request.Label,
            request.Country,
            request.City,
            request.Street,
            request.Building,
            request.Apartment,
            request.PostalCode,
            request.Phone,
            request.IsDefault);

        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateAddressResponse
        {
            Address = address.ToContractModel(),
        };
    }
}
