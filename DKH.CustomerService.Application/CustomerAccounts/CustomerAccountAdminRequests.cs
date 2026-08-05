using DKH.CustomerService.Contracts.Customer.Api.CustomerAccountAdmin.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;

namespace DKH.CustomerService.Application.CustomerAccounts;

public sealed record ListAdminCustomerAccountsQuery(
    string? Query,
    CustomerAccountStatus? Status,
    bool IncludeDeleted,
    int Page,
    int PageSize) : IRequest<ListCustomerAccountsResponse>;

public sealed record GetAdminCustomerAccountQuery(Guid AccountId, bool IncludeDeleted)
    : IRequest<AdminCustomerAccountModel>;

public sealed record ListAdminStorefrontMembershipsQuery(
    Guid AccountId,
    Guid? StorefrontId,
    StorefrontMembershipStatus? Status,
    bool IncludeDeleted,
    int Page,
    int PageSize) : IRequest<ListAccountStorefrontMembershipsResponse>;

public sealed record SetAdminCustomerAccountStatusCommand(
    Guid AccountId,
    CustomerAccountStatus Status) : IRequest<AdminCustomerAccountModel>;

public sealed record SetAdminStorefrontMembershipStatusCommand(
    Guid MembershipId,
    StorefrontMembershipStatus Status) : IRequest<AdminStorefrontMembershipModel>;

public sealed record ExportAdminCustomerAccountQuery(Guid AccountId, string? Format)
    : IRequest<ExportCustomerAccountResponse>;

public sealed record DeleteAdminCustomerAccountCommand(Guid AccountId) : IRequest;
