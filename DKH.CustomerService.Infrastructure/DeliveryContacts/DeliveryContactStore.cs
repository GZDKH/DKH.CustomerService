using System.Security.Cryptography;
using System.Text.Json;
using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using DKH.CustomerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Infrastructure.DeliveryContacts;

public sealed partial class DeliveryContactStore(AppDbContext db, IDeliveryContactScopeAuthorizer authorizer, TimeProvider clock) : IDeliveryContactStore
{
    public const string Authority = "customer.delivery-contact.v1";

    public async Task<DeliveryContactView> SealAsync(DeliveryContactActor actor, Guid addressId, Guid operationId, string requestDigest, CancellationToken cancellationToken)
    {
        var owner = await ResolveActorAsync(actor, cancellationToken);
        var address = await RequireAddressAsync(owner, actor.StorefrontId, addressId, cancellationToken);
        var rows = db.Set<DeliveryContactRevisionEntity>();
        var replay = await rows.AsNoTracking().SingleOrDefaultAsync(row => row.OwnerAccountId == owner && row.OperationId == operationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.RequestDigest != requestDigest || replay.AddressId != addressId || replay.StorefrontId != actor.StorefrontId)
            {
                throw Conflict();
            }

            await RequireContactAsync(Ref(replay), owner, actor.StorefrontId, cancellationToken);
            return new(Ref(replay), replay.LegacyProfileId);
        }

        var previous = await rows.Where(row => row.OwnerAccountId == owner && row.StorefrontId == actor.StorefrontId && row.AddressId == addressId)
            .Select(row => (long?)row.Revision).MaxAsync(cancellationToken) ?? 0;
        var contact = DeliveryContactRevisionEntity.Observe(owner, actor.StorefrontId, address, checked(previous + 1), operationId, Now(), requestDigest);
        rows.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        return new(Ref(contact), contact.LegacyProfileId);
    }

    public async Task<DeliveryContactAccessView> ResolveLiabilityAsync(DeliveryContactActor actor, DeliveryContactAuthority authority,
        Guid intendedPlanRevisionId, Guid legId, Guid operationId, Guid? accessId, string requestDigest, CancellationToken cancellationToken)
    {
        if (intendedPlanRevisionId == Guid.Empty || legId == Guid.Empty)
        {
            throw new ArgumentException("A plan and leg selector are required.");
        }

        var owner = await ResolveActorAsync(actor, cancellationToken);
        var (contact, _) = await RequireContactAsync(authority, owner, actor.StorefrontId, cancellationToken);
        // These are intended context selectors only. Customer does not attest that
        // the plan/duty exists or that the actor may accept it.
        var context = Hash(new { IntendedPlanRevisionId = intendedPlanRevisionId, LegId = legId });
        var lease = await AccessAsync(owner, contact, DeliveryContactPurpose.OwnLiabilityCoordination, context,
            requestDigest, operationId, accessId, Now().AddMinutes(5), null, null, cancellationToken);
        return new(lease.Id, lease.ExpiresAtUtc, 0);
    }

    public async Task<DeliveryContactConsentView> ConsentAsync(DeliveryContactActor actor, DeliveryContactAuthority authority,
        DeliveryContactScope scope, DateTimeOffset validUntilUtc, Guid operationId, string requestDigest, CancellationToken cancellationToken)
    {
        scope.Validate();
        var owner = await ResolveActorAsync(actor, cancellationToken);
        var (contact, _) = await RequireContactAsync(authority, owner, actor.StorefrontId, cancellationToken);
        validUntilUtc = validUntilUtc.ToUniversalTime().AddTicks(-(validUntilUtc.Ticks % 10));
        var rows = db.Set<DeliveryContactConsentEntity>();
        var replay = await rows.AsNoTracking().SingleOrDefaultAsync(row => row.OwnerAccountId == owner && row.OperationId == operationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.RequestDigest != requestDigest || replay.ContactRevisionId != contact.Id || replay.ScopeDigest != scope.Digest() || replay.ValidUntilUtc != validUntilUtc)
            {
                throw Conflict();
            }

            if (!replay.Permits(contact, scope, Now()))
            {
                throw Unavailable();
            }

            return ConsentView(replay);
        }

        var now = Now();
        if (validUntilUtc > now.AddMinutes(5))
        {
            throw new ArgumentException("Consent must expire within five minutes.");
        }

        var consent = DeliveryContactConsentEntity.Record(contact, scope, operationId, now, validUntilUtc, requestDigest);
        rows.Add(consent);
        await db.SaveChangesAsync(cancellationToken);
        return ConsentView(consent);
    }

    public async Task<DeliveryContactConsentView> RevokeAsync(DeliveryContactActor actor, Guid consentId, long expectedRevision,
        Guid operationId, string requestDigest, CancellationToken cancellationToken)
    {
        var owner = await ResolveActorAsync(actor, cancellationToken);
        // The authenticated account lock serializes every revocation operation,
        // including attempts to replay the same key against a different consent.
        var replay = await db.Set<DeliveryContactConsentEntity>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(row => row.OwnerAccountId == owner && row.RevokeOperationId == operationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.StorefrontId != actor.StorefrontId || !replay.MatchesRevocationReplay(consentId, operationId, expectedRevision, requestDigest))
            {
                throw Conflict();
            }

            return ConsentView(replay);
        }

        var consent = await db.Set<DeliveryContactConsentEntity>().SingleOrDefaultAsync(row => row.Id == consentId &&
            row.OwnerAccountId == owner && row.StorefrontId == actor.StorefrontId, cancellationToken) ?? throw Unavailable();
        // Revocation remains possible after the source address changed/deleted.
        consent.Revoke(operationId, expectedRevision, Now(), requestDigest);
        await db.SaveChangesAsync(cancellationToken);
        return ConsentView(consent);
    }

    public async Task<DeliveryRecipientContactView> ResolveRecipientAsync(DeliveryContactActor actor, DeliveryContactAuthority authority,
        Guid consentId, DeliveryContactScope scope, Guid operationId, Guid? accessId, string requestDigest, CancellationToken cancellationToken)
    {
        scope.Validate();
        var owner = await ResolveActorAsync(actor, cancellationToken);
        // This owner call must freshly bind actor, current duty/choice/scope,
        // actual recipient account/profile, workspace, purpose and expiry.
        // Its production default fails closed while that genuine boundary is absent.
        var decision = await authorizer.RevalidateAsync(owner, actor.StorefrontId, scope, cancellationToken);
        if (decision.StorefrontId != actor.StorefrontId || decision.Scope != scope ||
            decision.RecipientProfileId == Guid.Empty || decision.WorkspaceId == Guid.Empty || decision.ExpiresAtUtc <= Now())
        {
            throw Unavailable();
        }

        var retained = await db.Set<DeliveryContactRevisionEntity>().AsNoTracking().SingleOrDefaultAsync(row =>
            row.Id == authority.ResourceId && row.LegacyProfileId == decision.RecipientProfileId && row.StorefrontId == actor.StorefrontId, cancellationToken)
            ?? throw Unavailable();
        // Delivery attests only its actual profile binding. The account mapping
        // is resolved and rechecked by Customer, never inferred from UUID names.
        var (contact, address) = await RequireContactAsync(authority, retained.OwnerAccountId, actor.StorefrontId, cancellationToken);
        if (contact.LegacyProfileId != decision.RecipientProfileId)
        {
            throw Unavailable();
        }

        var consent = await db.Set<DeliveryContactConsentEntity>().FromSqlInterpolated($"SELECT * FROM delivery_contact_consents WHERE \"Id\" = {consentId} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Unavailable();
        if (!consent.Permits(contact, scope, Now()))
        {
            throw Unavailable();
        }

        var name = string.Join(" ", new[] { address.FirstName, address.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(name) || decision.CarrierRequiresPhone && string.IsNullOrWhiteSpace(address.Phone))
        {
            throw Unavailable();
        }

        var lease = await AccessAsync(owner, contact, DeliveryContactPurpose.LocalShipment, scope.Digest(),
            requestDigest, operationId, accessId,
            decision.ExpiresAtUtc, consent, decision.WorkspaceId, cancellationToken);
        return new(new(lease.Id, lease.ExpiresAtUtc, scope.ScopeRevision), name,
            address.Country, address.City, address.Province, address.Street, address.Building, address.Apartment,
            address.PostalCode, decision.CarrierRequiresPhone ? address.Phone : null);
    }

    private async Task<DeliveryContactLeaseEntity> AccessAsync(Guid actorId, DeliveryContactRevisionEntity contact,
        DeliveryContactPurpose purpose, string context, string requestHash, Guid operationId, Guid? accessId,
        DateTimeOffset authorityExpiry, DeliveryContactConsentEntity? consent, Guid? workspaceId, CancellationToken ct)
    {
        var rows = db.Set<DeliveryContactLeaseEntity>();
        var replay = accessId is { } id
            ? await rows.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct)
            : await rows.AsNoTracking().SingleOrDefaultAsync(row => row.ActorAccountId == actorId && row.OperationId == operationId, ct);
        if (replay is not null)
        {
            if (replay.OperationId != operationId || replay.RequestDigest != requestHash || replay.ContactRevisionId != contact.Id ||
                !replay.MatchesAccess(actorId, contact.StorefrontId, purpose, context, Now(), workspaceId) ||
                replay.ConsentId != consent?.Id || replay.ConsentRevision != consent?.Revision || replay.ExpiresAtUtc > authorityExpiry ||
                consent is not null && replay.ExpiresAtUtc > consent.ValidUntilUtc)
            {
                throw Unavailable();
            }

            return replay;
        }

        if (accessId is not null)
        {
            throw Unavailable();
        }

        var lease = DeliveryContactLeaseEntity.Issue(actorId, contact, purpose, context, requestHash, operationId, Now(), authorityExpiry, consent, workspaceId);
        rows.Add(lease);
        await db.SaveChangesAsync(ct);
        return lease;
    }

    private static DeliveryContactConsentView ConsentView(DeliveryContactConsentEntity row) => new(row.Id, row.Revision, row.ValidUntilUtc, row.RevokedAtUtc is not null);
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
