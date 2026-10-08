using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using DKH.CustomerService.Application.CustomerAccounts;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DKH.CustomerService.Api.Journal;

public static class CustomerJournalServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerJournalOwnership(this IServiceCollection services)
    {
        services.AddScoped<CustomerJournalOwnerResolver>();
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, CustomerJournalPrincipalOptions>();
        return services;
    }
}

internal sealed class CustomerJournalPrincipalOptions : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var previous = options.Events.OnTokenValidated;
        options.Events.OnTokenValidated = async context =>
        {
            await previous(context);
            if (context.Result?.Failure is null && context.Principal?.Identity?.IsAuthenticated == true)
            {
                CustomerJournalPrincipal.Capture(context);
            }
        };
    }
}

internal static class CustomerJournalPrincipal
{
    private static readonly object ValidatedIdentityKey = new();

    public static void Capture(TokenValidatedContext context)
    {
        var encoded = context.SecurityToken switch
        {
            JsonWebToken token => token.EncodedPayload,
            JwtSecurityToken token => token.RawPayload,
            _ => null,
        };
        if (encoded is null)
        {
            return;
        }

        // This is shape validation of the authenticated token, never another
        // signature validator and never a parse of an untrusted request token.
        using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(encoded));
        var root = payload.RootElement;
        if (String(root, "dkh_principal_purpose") != "personal:v1" ||
            String(root, "sub") is not { } subject || string.IsNullOrWhiteSpace(subject) ||
            subject.Length > 256 || subject != subject.Trim() ||
            String(root, "iss") is not { } issuer)
        {
            return;
        }

        context.HttpContext.Items[ValidatedIdentityKey] = new CustomerAccountIdentity(issuer, subject);
    }

    public static CustomerAccountIdentity Require(HttpContext context, IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication is required."));
        }

        var identity = Services.CustomerAccountIdentityResolver.Resolve(context.User, configuration);
        if (!context.Items.TryGetValue(ValidatedIdentityKey, out var value) ||
            value is not CustomerAccountIdentity validated ||
            identity.Subject != validated.Subject ||
            !IsTrustedIssuer(validated.Issuer, identity.Issuer, configuration) ||
            context.User.FindAll("sub").Count() != 1 ||
            context.User.FindAll("dkh_principal_purpose").Count() != 1 ||
            context.User.FindFirst("dkh_principal_purpose")?.Value != "personal:v1")
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Personal purpose is required."));
        }

        return identity;
    }

    private static bool IsTrustedIssuer(string validatedIssuer, string canonicalIssuer, IConfiguration configuration)
    {
        if (validatedIssuer == canonicalIssuer)
        {
            return true;
        }

        // Platform validates internal and browser-facing issuer URLs for the
        // same realm. Keep the existing internal canonical account namespace;
        // an arbitrary validator allowlist addition is not an owner authority.
        var external = configuration["Platform:Auth:Keycloak:ExternalAuthServerUrl"];
        var realm = configuration["Platform:Auth:Keycloak:Realm"];
        return !string.IsNullOrWhiteSpace(external) && !string.IsNullOrWhiteSpace(realm) &&
            validatedIssuer == $"{external.TrimEnd('/')}/realms/{realm.Trim('/')}";
    }

    private static string? String(JsonElement root, string name)
    {
        var properties = root.EnumerateObject().Where(property => property.NameEquals(name)).Take(2).ToArray();
        return properties.Length == 1 && properties[0].Value.ValueKind == JsonValueKind.String
            ? properties[0].Value.GetString()
            : null;
    }
}
