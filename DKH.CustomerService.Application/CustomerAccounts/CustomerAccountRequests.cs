using DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;

namespace DKH.CustomerService.Application.CustomerAccounts;

public sealed record CustomerAccountIdentity(string Issuer, string Subject);

public sealed record VerifiedCustomerAccountIdentity(
    string Issuer,
    string Subject,
    string VerifiedEmail,
    DateTime EmailVerifiedAt,
    string? FirstName,
    string? LastName,
    string PreferredLocale);

public sealed record EnsureCustomerAccountCommand(VerifiedCustomerAccountIdentity Identity)
    : IRequest<CustomerAccountModel>;

public sealed record GetCustomerAccountQuery(CustomerAccountIdentity Identity)
    : IRequest<CustomerAccountModel>;

public sealed record UpdateCustomerAccountCommand(
    CustomerAccountIdentity Identity,
    string? FirstName,
    string? LastName,
    string? PreferredLocale)
    : IRequest<CustomerAccountModel>;

public sealed record EnsureStorefrontMembershipCommand(
    CustomerAccountIdentity Identity,
    Guid StorefrontId,
    DateTime AuthenticatedAt)
    : IRequest<StorefrontMembershipModel>;

public sealed record ListStorefrontMembershipsQuery(
    CustomerAccountIdentity Identity,
    int Page,
    int PageSize)
    : IRequest<ListStorefrontMembershipsResponse>;

public sealed record ListLinkedCustomerIdentitiesQuery(
    CustomerAccountIdentity Identity,
    int Page,
    int PageSize)
    : IRequest<ListLinkedCustomerIdentitiesResponse>;

public sealed record ListConsolidatedWishlistEntriesQuery(
    CustomerAccountIdentity Identity,
    int Page,
    int PageSize)
    : IRequest<ListConsolidatedWishlistEntriesResponse>;

public sealed record DeleteStorefrontMembershipCommand(
    CustomerAccountIdentity Identity,
    Guid StorefrontId)
    : IRequest;

public sealed record DeleteCustomerAccountDataCommand(CustomerAccountIdentity Identity)
    : IRequest;
