using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Admin.UnblockCustomer;

public class UnblockCustomerCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UnblockCustomerCommand, UnblockCustomerResponse>
{
    public async Task<UnblockCustomerResponse> Handle(UnblockCustomerCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        profile.AccountStatus.Unblock();
        await repository.UpdateAsync(profile, cancellationToken);

        return new UnblockCustomerResponse
        {
            Success = true,
            Profile = profile.ToContractModel(),
        };
    }
}
