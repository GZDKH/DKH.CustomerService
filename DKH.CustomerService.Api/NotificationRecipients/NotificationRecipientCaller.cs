using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DKH.CustomerService.Api.NotificationRecipients;

public static class NotificationRecipientRegistration
{
    public static IServiceCollection AddCustomerNotificationRecipients(this IServiceCollection services)
    {
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, NotificationRecipientPrincipalOptions>();
        return services;
    }
}

internal sealed class NotificationRecipientPrincipalOptions : IPostConfigureOptions<JwtBearerOptions>
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
                NotificationRecipientCaller.Capture(context);
            }
        };
    }
}

/// <summary>Shape/provenance of an already Platform-validated Notification JWT, not a signature validator.
/// This machine DATA reader never changes the principal to the recipient.</summary>
internal sealed class NotificationRecipientCaller
{
    private const string Client = "dkh-notification-service";
    private static readonly object ValidatedKey = new();
    private readonly HttpContext _http;
    private readonly Stamp _stamp;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly string?[] _authorization;
    private readonly string?[] _storefront;
    private readonly PathString _path;

    private NotificationRecipientCaller(HttpContext http, Stamp stamp, IConfiguration configuration, TimeProvider clock)
    {
        _http = http;
        _stamp = stamp;
        _configuration = configuration;
        _clock = clock;
        _authorization = [.. http.Request.Headers.Authorization];
        _storefront = [.. http.Request.Headers["X-Storefront-Id"]];
        _path = http.Request.Path;
    }

    public DateTimeOffset TokenValidUntil => _stamp.ExpiresAt;

    public static void Capture(TokenValidatedContext context)
    {
        var encoded = context.SecurityToken switch
        {
            JsonWebToken token => token.EncodedPayload,
            JwtSecurityToken token => token.RawPayload,
            _ => null
        };
        if (encoded is null || context.Principal is null)
        {
            return;
        }

        using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(encoded));
        var root = payload.RootElement;
        var issuer = String(root, "iss");
        var subject = String(root, "sub");
        var exp = root.EnumerateObject().Where(value => value.NameEquals("exp")).ToArray();
        if (String(root, "dkh_principal_purpose") != "service:v1" || String(root, "azp") != Client ||
            String(root, "preferred_username") != "service-account-" + Client || string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(subject) || subject.Length > 256 || exp.Length != 1 ||
            exp[0].Value.ValueKind != JsonValueKind.Number || !exp[0].Value.TryGetInt64(out var seconds))
        {
            return;
        }

        try
        {
            context.HttpContext.Items[ValidatedKey] = new Stamp(context.Principal, issuer, subject, DateTimeOffset.FromUnixTimeSeconds(seconds));
        }
        catch (ArgumentOutOfRangeException)
        {
            // Invalid authenticated token shape cannot become this data-reader identity.
        }
    }

    public static NotificationRecipientCaller Require(HttpContext http, IConfiguration configuration, TimeProvider clock)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication is required."));
        }

        if (!http.Items.TryGetValue(ValidatedKey, out var value) || value is not Stamp stamp)
        {
            throw Denied();
        }

        var caller = new NotificationRecipientCaller(http, stamp, configuration, clock);
        caller.RequireCurrent(default);
        return caller;
    }

    public Task RequireCurrent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _http.RequestAborted.ThrowIfCancellationRequested();
        if (!ReferenceEquals(_http.User, _stamp.Principal) || _http.User.Identity?.IsAuthenticated != true ||
            Single(_http.User, "sub") != _stamp.Subject || Single(_http.User, "iss") != _stamp.Issuer ||
            Single(_http.User, "azp") != Client || Single(_http.User, "preferred_username") != "service-account-" + Client ||
            Single(_http.User, "dkh_principal_purpose") != "service:v1" ||
            !long.TryParse(Single(_http.User, "exp"), NumberStyles.None, CultureInfo.InvariantCulture, out var exp) ||
            exp != _stamp.ExpiresAt.ToUnixTimeSeconds() || !TrustedIssuer(_stamp.Issuer, _configuration) ||
            _stamp.ExpiresAt <= _clock.GetUtcNow() || _http.Request.Path != _path ||
            !_http.Request.Headers.Authorization.SequenceEqual(_authorization) ||
            !_http.Request.Headers["X-Storefront-Id"].SequenceEqual(_storefront))
        {
            throw Denied();
        }

        return Task.CompletedTask;
    }

    private static bool TrustedIssuer(string issuer, IConfiguration config)
    {
        var realm = config["Platform:Auth:Keycloak:Realm"];
        if (string.IsNullOrWhiteSpace(realm))
        {
            return false;
        }

        return new[] { config["Platform:Auth:Keycloak:AuthServerUrl"], config["Platform:Auth:Keycloak:ExternalAuthServerUrl"] }
            .Any(server => !string.IsNullOrWhiteSpace(server) && issuer == $"{server.TrimEnd('/')}/realms/{realm.Trim('/')}");
    }

    private static string? Single(ClaimsPrincipal principal, string name)
    {
        var claims = principal.FindAll(name).Take(2).ToArray();
        return claims.Length == 1 ? claims[0].Value : null;
    }

    private static string? String(JsonElement root, string name)
    {
        var properties = root.EnumerateObject().Where(value => value.NameEquals(name)).Take(2).ToArray();
        return properties.Length == 1 && properties[0].Value.ValueKind == JsonValueKind.String ? properties[0].Value.GetString() : null;
    }

    private static RpcException Denied() => new(new Status(StatusCode.PermissionDenied, "Notification recipient service identity is required."));
    private sealed record Stamp(ClaimsPrincipal Principal, string Issuer, string Subject, DateTimeOffset ExpiresAt);
}
