using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Domain.Events;
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

        var oldStatus = profile.AccountStatus.Status;
        profile.AccountStatus.Block(request.Reason, request.BlockedBy);

        profile.AddDomainEvent(new CustomerSegmentChangedDomainEvent(
            profile.Id,
            profile.StorefrontId,
            oldStatus,
            AccountStatusType.Blocked));

        await repository.UpdateAsync(profile, cancellationToken);

        return new BlockCustomerResponse
        {
            Success = true,
            Profile = profile.ToContractModel(),
        };
    }
}
