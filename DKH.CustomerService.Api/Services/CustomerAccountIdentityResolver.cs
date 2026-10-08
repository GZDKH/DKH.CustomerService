using System.Security.Claims;
using DKH.CustomerService.Application.CustomerAccounts;
using Grpc.Core;

namespace DKH.CustomerService.Api.Services;

internal static class CustomerAccountIdentityResolver
{
    public static CustomerAccountIdentity Resolve(ClaimsPrincipal principal, IConfiguration configuration)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication is required."));
        }

        var subject = principal.FindFirst("sub")?.Value;
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
}
