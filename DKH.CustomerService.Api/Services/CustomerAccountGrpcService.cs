using System.Security.Claims;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using DKH.Platform.Identity;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1.CustomerAccountService;

namespace DKH.CustomerService.Api.Services;

[Authorize]
public sealed class CustomerAccountGrpcService(
    IMediator mediator,
    IPlatformCurrentUser currentUser,
    IPlatformStorefrontContext storefrontContext,
    IConfiguration configuration)
    : ContractsService.CustomerAccountServiceBase
{
    public override Task<CustomerAccountModel> EnsureCustomerAccount(
        EnsureCustomerAccountRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new EnsureCustomerAccountCommand(ResolveVerifiedIdentity()),
            context.CancellationToken));

    public override Task<CustomerAccountModel> GetCustomerAccount(
        GetCustomerAccountRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new GetCustomerAccountQuery(ResolveIdentity()),
            context.CancellationToken));

    public override Task<CustomerAccountModel> UpdateCustomerAccount(
        UpdateCustomerAccountRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new UpdateCustomerAccountCommand(
                ResolveIdentity(),
                request.HasFirstName ? request.FirstName : null,
                request.HasLastName ? request.LastName : null,
                request.HasPreferredLocale ? request.PreferredLocale : null),
            context.CancellationToken));

    public override Task<StorefrontMembershipModel> EnsureStorefrontMembership(
        EnsureStorefrontMembershipRequest request,
        ServerCallContext context)
        => ExecuteAsync(async () =>
        {
            var verifiedIdentity = ResolveVerifiedIdentity();
            await mediator.Send(
                new EnsureCustomerAccountCommand(verifiedIdentity),
                context.CancellationToken);
            return await mediator.Send(
                new EnsureStorefrontMembershipCommand(
                    new CustomerAccountIdentity(verifiedIdentity.Issuer, verifiedIdentity.Subject),
                    RequireStorefrontId(),
                    DateTime.UtcNow),
                context.CancellationToken);
        });

    public override Task<ListStorefrontMembershipsResponse> ListStorefrontMemberships(
        ListStorefrontMembershipsRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new ListStorefrontMembershipsQuery(
                ResolveIdentity(),
                request.Page,
                request.PageSize),
            context.CancellationToken));

    public override Task<ListLinkedCustomerIdentitiesResponse> ListLinkedCustomerIdentities(
        ListLinkedCustomerIdentitiesRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new ListLinkedCustomerIdentitiesQuery(
                ResolveIdentity(),
                request.Page,
                request.PageSize),
            context.CancellationToken));

    public override Task<ListConsolidatedWishlistEntriesResponse> ListConsolidatedWishlistEntries(
        ListConsolidatedWishlistEntriesRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new ListConsolidatedWishlistEntriesQuery(
                ResolveIdentity(),
                request.Page,
                request.PageSize),
            context.CancellationToken));

    private VerifiedCustomerAccountIdentity ResolveVerifiedIdentity()
    {
        var identity = ResolveIdentity();
        var email = Claim(ClaimTypes.Email, "email");
        if (string.IsNullOrWhiteSpace(email) ||
            !bool.TryParse(Claim("email_verified"), out var emailVerified) ||
            !emailVerified)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                "A verified email claim is required."));
        }

        return new VerifiedCustomerAccountIdentity(
            identity.Issuer,
            identity.Subject,
            email,
            DateTime.UtcNow,
            Claim(ClaimTypes.GivenName, "given_name"),
            Claim(ClaimTypes.Surname, "family_name"),
            Claim("locale") ?? "en");
    }

    private CustomerAccountIdentity ResolveIdentity()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication is required."));
        }

        var subject = Claim("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authenticated subject is missing."));
        }

        var authServerUrl = configuration["Platform:Auth:Keycloak:AuthServerUrl"];
        var realm = configuration["Platform:Auth:Keycloak:Realm"];
        if (string.IsNullOrWhiteSpace(authServerUrl) || string.IsNullOrWhiteSpace(realm))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Trusted identity issuer is not configured."));
        }

        return new CustomerAccountIdentity(
            $"{authServerUrl.TrimEnd('/')}/realms/{realm.Trim('/')}",
            subject);
    }

    private Guid RequireStorefrontId()
        => storefrontContext.StorefrontId
           ?? throw new RpcException(new Status(
               StatusCode.FailedPrecondition,
               "Resolved storefront context is required."));

    private string? Claim(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = currentUser.GetClaim(claimType) ??
                        currentUser.Claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static async Task<TResponse> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            return await action();
        }
        catch (CustomerAccountNotFoundException exception)
        {
            throw new RpcException(new Status(StatusCode.NotFound, exception.Message));
        }
        catch (CustomerAccountConflictException exception)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message));
        }
        catch (CustomerAccountAccessException exception)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message));
        }
    }
}
