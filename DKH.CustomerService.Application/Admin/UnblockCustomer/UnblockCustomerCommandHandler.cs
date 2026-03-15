using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerCrud.v1;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Domain.Events;
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

        var oldStatus = profile.AccountStatus.Status;
        profile.AccountStatus.Unblock();

        profile.AddDomainEvent(new CustomerSegmentChangedDomainEvent(
            profile.Id,
            profile.StorefrontId,
            oldStatus,
            AccountStatusType.Active));

        await repository.UpdateAsync(profile, cancellationToken);

        return new UnblockCustomerResponse
        {
            Success = true,
            Profile = profile.ToContractModel(),
        };
    }
}
