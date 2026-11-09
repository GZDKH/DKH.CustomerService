using DKH.CustomerService.Api.NotificationRecipients;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Application.NotificationRecipients;
using DKH.CustomerService.Contracts.Customer.Api.NotificationRecipient.v2;
using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using EmailState = DKH.CustomerService.Contracts.Customer.Api.NotificationRecipient.v2.NotificationEmailEligibility;

namespace DKH.CustomerService.Api.Grpc.Services.NotificationRecipient.v2;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class NotificationRecipientGrpcService(CurrentNotificationRecipient recipients, IConfiguration configuration,
    TimeProvider clock, IPlatformStorefrontContext storefrontContext) : NotificationRecipientService.NotificationRecipientServiceBase
{
    public override async Task<NotificationRecipientModel> GetBudgetChangeRecipient(GetBudgetChangeRecipientRequest request,
        ServerCallContext context)
    {
        var http = context.GetHttpContext();
        var caller = NotificationRecipientCaller.Require(http, configuration, clock);
        Task Current(CancellationToken ct)
        {
            if (!ReferenceEquals(context.GetHttpContext(), http))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Original request caller changed."));
            }

            return caller.RequireCurrent(ct);
        }

        var original = request.Clone();
        using var window = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, http.RequestAborted);
        window.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            if (original.StorefrontId is null || original.CustomerProfileId is null)
            {
                throw new ArgumentException("Exact recipient identifiers are required.");
            }

            var storefront = original.StorefrontId.ToGuid();
            var profile = original.CustomerProfileId.ToGuid();
            var headers = context.RequestHeaders.Where(value => value.Key.Equals("x-storefront-id", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (storefront == Guid.Empty || profile == Guid.Empty || headers.Length > 1 ||
                headers.Length == 1 && (!Guid.TryParse(headers[0].Value, out var selected) || selected != storefront) ||
                storefrontContext.StorefrontId is { } current && current != storefront)
            {
                throw new ArgumentException("An exact consistent storefront/profile is required.");
            }

            var issuer = CustomerAccountEntity.NormalizeIdentityIssuer(original.OriginalActorIssuer);
            var external = configuration["Platform:Auth:Keycloak:ExternalAuthServerUrl"];
            var server = configuration["Platform:Auth:Keycloak:AuthServerUrl"];
            var realm = configuration["Platform:Auth:Keycloak:Realm"];
            if (!string.IsNullOrWhiteSpace(external) && !string.IsNullOrWhiteSpace(server) && !string.IsNullOrWhiteSpace(realm) &&
                issuer == CustomerAccountEntity.NormalizeIdentityIssuer($"{external.TrimEnd('/')}/realms/{realm.Trim('/')}"))
            {
                issuer = CustomerAccountEntity.NormalizeIdentityIssuer($"{server.TrimEnd('/')}/realms/{realm.Trim('/')}");
            }

            var identity = new CustomerAccountIdentity(issuer, CustomerAccountEntity.NormalizeIdentitySubject(original.OriginalActorSubject));
            var source = await recipients.ReadAsync(identity, storefront, profile, caller.TokenValidUntil, Current, window.Token);
            await Current(window.Token);
            var result = new NotificationRecipientModel
            {
                CustomerProfileId = GuidValue.FromGuid(source.CustomerProfileId),
                StorefrontId = GuidValue.FromGuid(source.StorefrontId),
                CanonicalIdentityIssuer = source.CanonicalIssuer,
                CanonicalIdentitySubject = source.CanonicalSubject,
                Email = source.Email,
                EmailEligibility = (EmailState)(int)source.EmailEligibility,
                EmailNotificationsEnabled = source.EmailNotificationsEnabled,
                TelegramNotificationsEnabled = source.TelegramNotificationsEnabled,
                SmsNotificationsEnabled = source.SmsNotificationsEnabled,
                PreferredLanguage = source.PreferredLanguage,
                ObservedAt = Timestamp.FromDateTimeOffset(source.ObservedAt),
                ReadValidUntil = Timestamp.FromDateTimeOffset(source.ReadValidUntil)
            };
            if (source.EmailVerifiedAt is { } verifiedAt)
            {
                result.EmailVerifiedAt = Timestamp.FromDateTimeOffset(verifiedAt);
            }

            await Current(window.Token);
            return result;
        }
        catch (Exception error) when (error is ArgumentException or FormatException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A valid original identity and exact recipient scope are required."));
        }
        catch (Exception error) when (error is CustomerAccountNotFoundException or CustomerAccountAccessException)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Notification recipient is unavailable."));
        }
        catch (NotificationRecipientChangedException)
        {
            throw new RpcException(new Status(StatusCode.Aborted, "Current notification recipient changed or expired."));
        }
    }
}
