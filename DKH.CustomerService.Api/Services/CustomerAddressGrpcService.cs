using System.Security.Claims;
using System.Text;
using DKH.CustomerService.Application.Addresses.CreateAddress;
using DKH.CustomerService.Application.Addresses.DeleteAddress;
using DKH.CustomerService.Application.Addresses.GetAddress;
using DKH.CustomerService.Application.Addresses.GetDefaultAddress;
using DKH.CustomerService.Application.Addresses.ListAddresses;
using DKH.CustomerService.Application.Addresses.PermanentlyDeleteAddress;
using DKH.CustomerService.Application.Addresses.RestoreAddress;
using DKH.CustomerService.Application.Addresses.SetDefaultAddress;
using DKH.CustomerService.Application.Addresses.UpdateAddress;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using DKH.Platform.Authentication.Keycloak.Backend;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Grpc.Extensions;
using DKH.Platform.MultiTenancy;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1.CustomerAddressManagementService;

namespace DKH.CustomerService.Api.Services;

[Authorize(Policy = CustomerServiceAuthorizationPolicies.CustomerSelfAccess)]
public class CustomerAddressGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext)
    : ContractsService.CustomerAddressManagementServiceBase
{
    [RequireCallerMatchesClaim("UserId")]
    public override async Task<ListAddressesResponse> ListAddresses(ListAddressesRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await SendForOwnerAsync(
            new ListAddressesQuery(
                storefrontId,
                request.UserId,
                request.HasSoftDeleteFilter
                    ? request.SoftDeleteFilter.ToDomain()
                    : Platform.Domain.Enums.PlatformSoftDeleteFilter.ActiveOnly),
            context);
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<GetAddressResponse> GetAddress(GetAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var addressId = request.AddressId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Address ID is required"));

        return await SendForOwnerAsync(new GetAddressQuery(storefrontId, request.UserId, addressId), context);
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<CreateAddressResponse> CreateAddress(CreateAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await SendForOwnerAsync(
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
                request.IsDefault,
                string.IsNullOrEmpty(request.FirstName) ? null : request.FirstName,
                string.IsNullOrEmpty(request.LastName) ? null : request.LastName,
                string.IsNullOrEmpty(request.Province) ? null : request.Province,
                string.IsNullOrEmpty(request.Company) ? null : request.Company),
            context);
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<UpdateAddressResponse> UpdateAddress(UpdateAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var addressId = request.AddressId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Address ID is required"));

        return await SendForOwnerAsync(
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
                request.HasPhone ? request.Phone : null,
                request.HasFirstName ? request.FirstName : null,
                request.HasLastName ? request.LastName : null,
                request.HasProvince ? request.Province : null,
                request.HasCompany ? request.Company : null),
            context);
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<DeleteAddressResponse> DeleteAddress(DeleteAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var addressId = request.AddressId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Address ID is required"));

        return await SendForOwnerAsync(new DeleteAddressCommand(storefrontId, request.UserId, addressId), context);
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<SetDefaultAddressResponse> SetDefaultAddress(SetDefaultAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var addressId = request.AddressId?.ToGuid()
            ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Address ID is required"));

        return await SendForOwnerAsync(new SetDefaultAddressCommand(storefrontId, request.UserId, addressId), context);
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<GetDefaultAddressResponse> GetDefaultAddress(GetDefaultAddressRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        return await SendForOwnerAsync(new GetDefaultAddressQuery(storefrontId, request.UserId), context);
    }

    [Authorize(Policy = CustomerServiceAuthorizationPolicies.CustomerAccess)]
    public override async Task<Contracts.Customer.Models.CustomerAddress.v1.CustomerAddressModel> RestoreAddress(
        RestoreAddressRequest request,
        ServerCallContext context)
    {
        var addressId = request.AddressId.ToGuid();
        var entity = await mediator.Send(new RestoreAddressCommand(addressId), context.CancellationToken);
        return entity.ToContractModel();
    }

    [Authorize(Policy = CustomerServiceAuthorizationPolicies.CustomerAccess)]
    public override async Task<Empty> PermanentlyDeleteAddress(
        PermanentlyDeleteAddressRequest request,
        ServerCallContext context)
    {
        var addressId = request.AddressId.ToGuid();
        await mediator.Send(new PermanentlyDeleteAddressCommand(addressId), context.CancellationToken);
        return new Empty();
    }

    private Guid ResolveStorefrontId(GuidValue? requestStorefrontId)
    {
        var current = storefrontContext.StorefrontId;
        var selected = requestStorefrontId is not null && Guid.TryParse(requestStorefrontId.Value, out var supplied)
            ? supplied : requestStorefrontId is null ? current : null;
        if (selected is null || selected == Guid.Empty || current is not null && current != selected)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A single current storefront is required."));
        }

        return selected.Value;
    }

    private async Task<T> SendForOwnerAsync<T>(IRequest<T> request, ServerCallContext context)
    {
        // Existing authorization and RequireCallerMatchesClaim establish the caller.
        // Keep that exact request-local caller through dispatch, including errors.
        var http = context.GetHttpContext();
        var principal = http.User;
        var identity = principal.Identity;
        var caller = SnapshotCaller(principal);
        var authorization = http.Request.Headers.Authorization.ToArray();
        var path = http.Request.Path;
        var storefront = storefrontContext.StorefrontId;
        void RequireCurrent()
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            http.RequestAborted.ThrowIfCancellationRequested();
            var current = context.GetHttpContext();
            if (!ReferenceEquals(http, current) || !ReferenceEquals(principal, current.User)
                || !ReferenceEquals(identity, current.User.Identity) || current.User.Identity?.IsAuthenticated != true
                || !caller.AsSpan().SequenceEqual(SnapshotCaller(current.User)) || current.Request.Path != path
                || !authorization.SequenceEqual(current.Request.Headers.Authorization.ToArray(), StringComparer.Ordinal)
                || storefrontContext.StorefrontId != storefront)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "ADDRESS_CALLER_CHANGED"));
            }
        }

        RequireCurrent();
        try
        {
            return await mediator.Send(request, context.CancellationToken);
        }
        finally
        {
            // A late disclosure refusal does not undo an already committed mutation.
            RequireCurrent();
        }
    }

    private static byte[] SnapshotCaller(ClaimsPrincipal principal)
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true);
        principal.WriteTo(writer);
        writer.Flush();
        return buffer.ToArray();
    }
}
