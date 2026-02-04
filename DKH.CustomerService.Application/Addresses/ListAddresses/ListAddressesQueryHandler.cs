using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using MediatR;

namespace DKH.CustomerService.Application.Addresses.ListAddresses;

public class ListAddressesQueryHandler(ICustomerRepository repository)
    : IRequestHandler<ListAddressesQuery, ListAddressesResponse>
{
    public async Task<ListAddressesResponse> Handle(ListAddressesQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdWithAddressesAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken);

        var response = new ListAddressesResponse();

        if (profile is not null)
        {
            response.Addresses.AddRange(profile.Addresses.Select(a => a.ToContractModel()));
        }

        return response;
    }
}
