using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using Grpc.Core;

namespace DKH.CustomerService.Application.Admin.BlockCustomer;

public class BlockCustomerCommandHandler(ICustomerRepository repository)
    : IRequestHandler<BlockCustomerCommand, BlockCustomerResponse>
{
    public async Task<BlockCustomerResponse> Handle(BlockCustomerCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(
            request.StorefrontId,
            request.UserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        profile.AccountStatus.Block(request.Reason, request.BlockedBy);
        await repository.UpdateAsync(profile, cancellationToken);

        return new BlockCustomerResponse
        {
            Success = true,
            Profile = profile.ToContractModel(),
        };
    }
}
