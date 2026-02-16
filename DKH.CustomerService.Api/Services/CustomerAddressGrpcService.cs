using DKH.CustomerService.Application.Addresses.CreateAddress;
using DKH.CustomerService.Application.Addresses.DeleteAddress;
using DKH.CustomerService.Application.Addresses.GetAddress;
using DKH.CustomerService.Application.Addresses.GetDefaultAddress;
using DKH.CustomerService.Application.Addresses.ListAddresses;
using DKH.CustomerService.Application.Addresses.SetDefaultAddress;
using DKH.CustomerService.Application.Addresses.UpdateAddress;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1.CustomerAddressManagementService;

namespace DKH.CustomerService.Api.Services;

public class CustomerAddressGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext)
    : ContractsService.CustomerAddressManagementServiceBase
{
    public override async Task<ListAddressesResponse> ListAddresses(ListAddressesRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new ListAddressesQuery(storefrontId, request.UserId), context.CancellationToken);
    }

    public override async Task<GetAddressResponse> GetAddress(GetAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var addressId = request.AddressId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Address ID is required"));

        return await mediator.Send(new GetAddressQuery(storefrontId, request.UserId, addressId), context.CancellationToken);
    }

    public override async Task<CreateAddressResponse> CreateAddress(CreateAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(
            new CreateAddressCommand(
                storefrontId,
                request.UserId,
                request.Label,
                request.Country,
                request.City,
                request.Street,
                request.Building,
                request.Apartment,
                request.PostalCode,
                request.Phone,
                request.IsDefault),
            context.CancellationToken);
    }

    public override async Task<UpdateAddressResponse> UpdateAddress(UpdateAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var addressId = request.AddressId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Address ID is required"));

        return await mediator.Send(
            new UpdateAddressCommand(
                storefrontId,
                request.UserId,
                addressId,
                request.HasLabel ? request.Label : null,
                request.HasCountry ? request.Country : null,
                request.HasCity ? request.City : null,
                request.HasStreet ? request.Street : null,
                request.HasBuilding ? request.Building : null,
                request.HasApartment ? request.Apartment : null,
                request.HasPostalCode ? request.PostalCode : null,
                request.HasPhone ? request.Phone : null),
            context.CancellationToken);
    }

    public override async Task<DeleteAddressResponse> DeleteAddress(DeleteAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var addressId = request.AddressId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Address ID is required"));

        return await mediator.Send(new DeleteAddressCommand(storefrontId, request.UserId, addressId), context.CancellationToken);
    }

    public override async Task<SetDefaultAddressResponse> SetDefaultAddress(SetDefaultAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var addressId = request.AddressId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Address ID is required"));

        return await mediator.Send(new SetDefaultAddressCommand(storefrontId, request.UserId, addressId), context.CancellationToken);
    }

    public override async Task<GetDefaultAddressResponse> GetDefaultAddress(GetDefaultAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await mediator.Send(new GetDefaultAddressQuery(storefrontId, request.UserId), context.CancellationToken);
    }

    private Guid ResolveStorefrontId(GuidValue? requestStorefrontId)
    {
        if (requestStorefrontId is not null)
        {
            return requestStorefrontId.ToGuid();
        }

        return storefrontContext.StorefrontId
               ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required"));
    }
}
