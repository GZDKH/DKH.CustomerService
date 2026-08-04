using DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1.CustomerAccountService;

namespace DKH.CustomerService.Api.Services;

[Authorize(Policy = CustomerServiceAuthorizationPolicies.CustomerAccess)]
public sealed class CustomerAccountGrpcService : ContractsService.CustomerAccountServiceBase
{
    public override Task<CustomerAccountModel> EnsureCustomerAccount(
        EnsureCustomerAccountRequest request,
        ServerCallContext context)
        => NotImplemented<CustomerAccountModel>();

    public override Task<CustomerAccountModel> GetCustomerAccount(
        GetCustomerAccountRequest request,
        ServerCallContext context)
        => NotImplemented<CustomerAccountModel>();

    public override Task<CustomerAccountModel> UpdateCustomerAccount(
        UpdateCustomerAccountRequest request,
        ServerCallContext context)
        => NotImplemented<CustomerAccountModel>();

    public override Task<StorefrontMembershipModel> EnsureStorefrontMembership(
        EnsureStorefrontMembershipRequest request,
        ServerCallContext context)
        => NotImplemented<StorefrontMembershipModel>();

    public override Task<ListStorefrontMembershipsResponse> ListStorefrontMemberships(
        ListStorefrontMembershipsRequest request,
        ServerCallContext context)
        => NotImplemented<ListStorefrontMembershipsResponse>();

    public override Task<ListLinkedCustomerIdentitiesResponse> ListLinkedCustomerIdentities(
        ListLinkedCustomerIdentitiesRequest request,
        ServerCallContext context)
        => NotImplemented<ListLinkedCustomerIdentitiesResponse>();

    public override Task<ListConsolidatedWishlistEntriesResponse> ListConsolidatedWishlistEntries(
        ListConsolidatedWishlistEntriesRequest request,
        ServerCallContext context)
        => NotImplemented<ListConsolidatedWishlistEntriesResponse>();

    private static Task<TResponse> NotImplemented<TResponse>()
        => throw new RpcException(new Status(StatusCode.Unimplemented, "Customer account API is not implemented."));
}
