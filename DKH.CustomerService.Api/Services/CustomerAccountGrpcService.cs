using System.Security.Claims;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1.CustomerAccountService;

namespace DKH.CustomerService.Api.Services;

[Authorize]
public sealed class CustomerAccountGrpcService(
    IMediator mediator,
    IPlatformStorefrontContext storefrontContext,
    IOptions<PlatformStorefrontContextOptions> storefrontContextOptions,
    IConfiguration configuration)
    : ContractsService.CustomerAccountServiceBase
{
    public override Task<CustomerAccountModel> EnsureCustomerAccount(
        EnsureCustomerAccountRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new EnsureCustomerAccountCommand(ResolveVerifiedIdentity(context)),
            context.CancellationToken));

    public override Task<CustomerAccountModel> GetCustomerAccount(
        GetCustomerAccountRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new GetCustomerAccountQuery(ResolveIdentity(context)),
            context.CancellationToken));

    public override Task<CustomerAccountModel> UpdateCustomerAccount(
        UpdateCustomerAccountRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new UpdateCustomerAccountCommand(
                ResolveIdentity(context),
                request.HasFirstName ? request.FirstName : null,
                request.HasLastName ? request.LastName : null,
                request.HasPreferredLocale ? request.PreferredLocale : null),
            context.CancellationToken));

    public override Task<StorefrontMembershipModel> EnsureStorefrontMembership(
        EnsureStorefrontMembershipRequest request,
        ServerCallContext context)
        => ExecuteAsync(async () =>
        {
            var verifiedIdentity = ResolveVerifiedIdentity(context);
            await mediator.Send(
                new EnsureCustomerAccountCommand(verifiedIdentity),
                context.CancellationToken);
            return await mediator.Send(
                new EnsureStorefrontMembershipCommand(
                    new CustomerAccountIdentity(verifiedIdentity.Issuer, verifiedIdentity.Subject),
                    RequireStorefrontId(context),
                    DateTime.UtcNow),
                context.CancellationToken);
        });

    public override Task<ListStorefrontMembershipsResponse> ListStorefrontMemberships(
        ListStorefrontMembershipsRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new ListStorefrontMembershipsQuery(
                ResolveIdentity(context),
                request.Page,
                request.PageSize),
            context.CancellationToken));

    public override Task<ListLinkedCustomerIdentitiesResponse> ListLinkedCustomerIdentities(
        ListLinkedCustomerIdentitiesRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new ListLinkedCustomerIdentitiesQuery(
                ResolveIdentity(context),
                request.Page,
                request.PageSize),
            context.CancellationToken));

    public override Task<ListConsolidatedWishlistEntriesResponse> ListConsolidatedWishlistEntries(
        ListConsolidatedWishlistEntriesRequest request,
        ServerCallContext context)
        => ExecuteAsync(() => mediator.Send(
            new ListConsolidatedWishlistEntriesQuery(
                ResolveIdentity(context),
                request.Page,
                request.PageSize),
            context.CancellationToken));

    private VerifiedCustomerAccountIdentity ResolveVerifiedIdentity(ServerCallContext context)
    {
        var principal = context.GetHttpContext().User;
        var identity = ResolveIdentity(principal);
        var email = Claim(principal, ClaimTypes.Email, "email");
        if (string.IsNullOrWhiteSpace(email) ||
            !bool.TryParse(Claim(principal, "email_verified"), out var emailVerified) ||
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
            Claim(principal, ClaimTypes.GivenName, "given_name"),
            Claim(principal, ClaimTypes.Surname, "family_name"),
            Claim(principal, "locale") ?? "en");
    }

    private CustomerAccountIdentity ResolveIdentity(ServerCallContext context)
        => ResolveIdentity(context.GetHttpContext().User);

    private CustomerAccountIdentity ResolveIdentity(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication is required."));
        }

        var subject = Claim(principal, "sub");
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

    private Guid RequireStorefrontId(ServerCallContext context)
    {
        if (storefrontContext.StorefrontId is { } resolvedStorefrontId)
        {
            return resolvedStorefrontId;
        }

        var headerName = storefrontContextOptions.Value.HeaderName;
        Guid? parsedStorefrontId = null;
        var hasMetadataValue = false;

        foreach (var entry in context.RequestHeaders.Where(entry =>
                     string.Equals(entry.Key, headerName, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var value in entry.Value.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                hasMetadataValue = true;
                if (!Guid.TryParse(value, out var candidateStorefrontId) ||
                    parsedStorefrontId is { } existingStorefrontId && existingStorefrontId != candidateStorefrontId)
                {
                    throw MissingStorefrontContext();
                }

                parsedStorefrontId = candidateStorefrontId;
            }
        }

        if (hasMetadataValue && parsedStorefrontId is { } resolvedMetadataStorefrontId)
        {
            return resolvedMetadataStorefrontId;
        }

        throw MissingStorefrontContext();
    }

    private static RpcException MissingStorefrontContext()
        => new(new Status(
            StatusCode.FailedPrecondition,
            "Resolved storefront context is required."));

    private static string? Claim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
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
