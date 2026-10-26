using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using DKH.DeliveryService.Contracts.Delivery.Api.GroupContactAuthority.v2;
using DKH.DeliveryService.Contracts.Delivery.Models.v2;
using DKH.Platform.Grpc.Common.Types;
using Grpc.Core;

namespace DKH.CustomerService.Api.DeliveryContacts;

/// <summary>Fresh Delivery-owned authorization under the same validated human.
/// A published contract alone never establishes participant authority.</summary>
public sealed class DeliveryContactScopeAuthorizer(
    GroupContactAuthorityService.GroupContactAuthorityServiceClient client,
    IHttpContextAccessor httpContextAccessor, IConfiguration configuration, TimeProvider clock) : IDeliveryContactScopeAuthorizer
{
    public async Task<DeliveryContactScopeDecision> RevalidateAsync(Guid actorAccountId, Guid storefrontId,
        DeliveryContactScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        scope.Validate();
        if (actorAccountId == Guid.Empty || storefrontId == Guid.Empty || httpContextAccessor.HttpContext is not { } context)
        {
            throw Unavailable();
        }

        // ResolveActorAsync already bound this account to the authenticated
        // Customer identity. Delivery independently derives its caller from the
        // SAME human bearer; no account, grant or recipient selector is sent.
        var caller = new DeliveryContactCallerFence(context, () => httpContextAccessor.HttpContext, configuration, clock);
        var authorization = context.Request.Headers.Authorization;
        if (authorization.Count != 1 || authorization[0] is not { } bearer ||
            !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(bearer[7..]))
        {
            throw Unavailable();
        }

        // These headers select the owner scope; Delivery must independently
        // authorize the same human against its current records.
        var workspaces = context.Request.Headers["X-Workspace-Id"];
        if (workspaces.Count != 1 || !Guid.TryParse(workspaces[0], out var workspaceId) || workspaceId == Guid.Empty)
        {
            throw Unavailable();
        }

        var selectors = new Metadata
        {
            { "x-workspace-id", workspaceId.ToString("D") },
            { "x-storefront-id", storefrontId.ToString("D") },
        };
        RecipientContactScopeAuthorizationModel response;
        try
        {
            using var call = client.RevalidateRecipientContactScopeAsync(new() { Scope = Map(scope) }, headers: selectors,
                deadline: DateTimeOffset.Compare(caller.OriginalTokenExpiry, clock.GetUtcNow().AddSeconds(10)) < 0
                    ? caller.OriginalTokenExpiry.UtcDateTime : clock.GetUtcNow().AddSeconds(10).UtcDateTime,
                cancellationToken: cancellationToken);
            response = await call.ResponseAsync;
        }
        catch (RpcException) when (!cancellationToken.IsCancellationRequested)
        {
            // Unimplemented (current producer), revoked, denied, malformed or
            // unavailable owner responses never permit private contact access.
            throw Unavailable();
        }
        finally
        {
            // Includes RPC failure and disposal. Caller changes cannot disclose
            // either positive DATA or a stale owner-specific refusal.
            caller.RequireCurrent(cancellationToken);
        }

        try
        {
            if (response is null || ReadScope(response.Scope) != scope || Id(response.StorefrontId) != storefrontId ||
                Id(response.WorkspaceId) != workspaceId)
            {
                throw Unavailable();
            }

            var expires = response.ExpiresAt?.ToDateTimeOffset() ?? throw Unavailable();
            var now = clock.GetUtcNow();
            if (expires <= now || expires > now.AddMinutes(5) || expires > caller.OriginalTokenExpiry)
            {
                throw Unavailable();
            }

            return new(scope, Id(response.RecipientProfileBinding), storefrontId, Id(response.WorkspaceId),
                response.CarrierRequiresPhone, expires, Id(response.RecipientConsentRef), Id(response.PrivateContactRef), ReadActor(response.ActingActorRef));
        }
        catch (ArgumentException)
        {
            throw Unavailable();
        }
        catch (InvalidOperationException)
        {
            throw Unavailable();
        }
    }

    private static DeliveryContactActingActor ReadActor(ActorRefModel? value)
    {
        if (value?.IdentityCase == ActorRefModel.IdentityOneofCase.CustomerIdentityId)
        {
            return new(Id(value.CustomerIdentityId), null);
        }

        if (value?.IdentityCase != ActorRefModel.IdentityOneofCase.CounterpartyAuthorityRef || value.CounterpartyAuthorityRef is not { } party)
        {
            // Legacy absent tag9, unknown and service actor variants cannot
            // turn absence into represented-company authority.
            throw Unavailable();
        }

        var authority = new DeliveryContactAuthority(party.Authority, Id(party.ResourceId), party.Revision, party.Digest);
        authority.Validate();
        if (authority.Authority != "counterparty.party")
        {
            throw Unavailable();
        }

        return new(null, authority);
    }

    private static GroupContactScopeModel Map(DeliveryContactScope value) => new()
    {
        GroupId = Id(value.GroupId),
        AllocationPublicRef = value.AllocationPublicRef,
        PlanRevisionId = Id(value.PlanRevisionId),
        ScopeRevision = value.ScopeRevision,
        ChoiceRevisionId = Id(value.ChoiceRevisionId),
        Purpose = GroupContactPurpose.LocalShipment,
        DutyAcceptance = new AuthorityRefModel
        {
            Authority = value.Duty.Authority,
            ResourceId = Id(value.Duty.ResourceId),
            Revision = value.Duty.Revision,
            Digest = value.Duty.Digest,
        },
    };

    private static DeliveryContactScope ReadScope(GroupContactScopeModel? value)
    {
        if (value is null || value.Purpose != GroupContactPurpose.LocalShipment || value.DutyAcceptance is null)
        {
            throw Unavailable();
        }

        var duty = value.DutyAcceptance;
        var scope = new DeliveryContactScope(Id(value.GroupId), value.AllocationPublicRef, Id(value.PlanRevisionId),
            value.ScopeRevision, Id(value.ChoiceRevisionId),
            new DeliveryContactAuthority(duty.Authority, Id(duty.ResourceId), duty.Revision, duty.Digest));
        scope.Validate();
        return scope;
    }

    private static GuidValue Id(Guid value) => new() { Value = value.ToString("D") };
    private static Guid Id(GuidValue? value) => value is not null && Guid.TryParse(value.Value, out var id) && id != Guid.Empty
        ? id : throw Unavailable();
    private static DeliveryContactFailureException Unavailable() => new("AUTHORITY_UNAVAILABLE");
}
