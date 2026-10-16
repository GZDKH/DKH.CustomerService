using DKH.CustomerService.Api.Journal;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Contracts.Customer.Api.CurrentProfileBinding.v2;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DKH.CustomerService.Api.Grpc.Services.CurrentProfileBinding.v2;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = CustomerServiceAuthorizationPolicies.CustomerSelfAccess)]
public sealed class CurrentProfileBindingGrpcService(CustomerJournalOwnerResolver ownerResolver,
    CurrentActiveProfileBinding bindings, IPlatformStorefrontContext storefrontContext,
    IOptions<PlatformStorefrontContextOptions> storefrontOptions)
    : CurrentProfileBindingService.CurrentProfileBindingServiceBase
{
    public override async Task<ActiveProfileBindingModel> GetMyActiveProfileBinding(
        GetMyActiveProfileBindingRequest request, ServerCallContext context)
    {
        var identity = ownerResolver.RequireIdentity(context);
        var storefrontId = RequireStorefront(context);
        try
        {
            var binding = await bindings.GetAsync(identity, storefrontId, context.CancellationToken);
            return new ActiveProfileBindingModel
            {
                CustomerProfileId = new GuidValue { Value = binding.CustomerProfileId.ToString("D") },
                StorefrontId = new GuidValue { Value = binding.StorefrontId.ToString("D") }
            };
        }
        catch (CustomerAccountNotFoundException)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "ACTIVE_PROFILE_UNAVAILABLE"));
        }
        catch (CustomerAccountAccessException)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "ACTIVE_PROFILE_UNAVAILABLE"));
        }
    }

    private Guid RequireStorefront(ServerCallContext context)
    {
        var values = context.RequestHeaders.Where(entry =>
            entry.Key.Equals(storefrontOptions.Value.HeaderName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (values.Length > 1 || values.Length == 1 && (!Guid.TryParse(values[0].Value, out var selected) ||
            selected == Guid.Empty || storefrontContext.StorefrontId is { } resolved && resolved != selected))
        {
            throw InvalidStorefront();
        }

        if (storefrontContext.StorefrontId is { } current && current != Guid.Empty)
        {
            return current;
        }

        return values.Length == 1 && Guid.TryParse(values[0].Value, out var id) && id != Guid.Empty
            ? id : throw InvalidStorefront();
    }

    private static RpcException InvalidStorefront()
        => new(new Status(StatusCode.InvalidArgument, "A single current storefront is required."));
}
