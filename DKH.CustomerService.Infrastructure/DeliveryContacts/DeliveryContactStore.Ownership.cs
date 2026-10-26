using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using DKH.CustomerService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Infrastructure.DeliveryContacts;

public sealed partial class DeliveryContactStore
{
    private async Task<Guid> ResolveActorAsync(DeliveryContactActor actor, CancellationToken ct)
        => (await ResolveActorBindingAsync(actor, ct)).AccountId;

    private async Task<(Guid AccountId, Guid ProfileId)> ResolveActorBindingAsync(DeliveryContactActor actor, CancellationToken ct)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Contact access requires the shared Platform transaction boundary.");
        }

        if (actor.StorefrontId == Guid.Empty)
        {
            throw Unavailable();
        }

        var issuer = CustomerAccountEntity.NormalizeIdentityIssuer(actor.Identity.Issuer);
        var subject = CustomerAccountEntity.NormalizeIdentitySubject(actor.Identity.Subject);
        var now = clock.GetUtcNow().UtcDateTime;
        var accounts = db.CustomerAccounts.IgnoreQueryFilters().AsNoTracking();
        var primary = accounts.Where(row => row.IdentityIssuer == issuer && row.IdentitySubject == subject);
        var aliases = db.LinkedCustomerIdentities.IgnoreQueryFilters().AsNoTracking()
            .Where(row => !row.IsDeleted && row.ProviderAuthority == issuer && row.ProviderSubject == subject &&
                row.VerifiedAt > DateTime.UnixEpoch && row.VerifiedAt <= now)
            .Join(accounts, row => row.CustomerAccountId, account => account.Id, (_, account) => account);
        var matches = await primary.Union(aliases).Take(2).ToArrayAsync(ct);
        if (matches.Length != 1 || matches[0].IsDeleted || matches[0].Status != CustomerAccountStatusType.Active)
        {
            throw Unavailable();
        }

        var id = matches[0].Id;
        var locked = await db.CustomerAccounts.FromSqlInterpolated($"SELECT * FROM customer_accounts WHERE \"Id\" = {id} FOR UPDATE")
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(ct);
        if (locked is null || locked.IsDeleted || locked.Status != CustomerAccountStatusType.Active)
        {
            throw Unavailable();
        }

        // Identity aliases can be revoked independently of account status.
        var stillPrimary = locked.IdentityIssuer == issuer && locked.IdentitySubject == subject;
        var stillAlias = await db.LinkedCustomerIdentities.IgnoreQueryFilters().AsNoTracking().AnyAsync(row =>
            !row.IsDeleted && row.CustomerAccountId == id && row.ProviderAuthority == issuer && row.ProviderSubject == subject &&
            row.VerifiedAt > DateTime.UnixEpoch && row.VerifiedAt <= now, ct);
        if (!stillPrimary && !stillAlias)
        {
            throw Unavailable();
        }

        var profileId = await RequireProfileAsync(id, actor.StorefrontId, ct);
        return (id, profileId);
    }

    private async Task<Guid> RequireProfileAsync(Guid ownerId, Guid storefrontId, CancellationToken ct)
    {
        var account = await db.CustomerAccounts.FromSqlInterpolated($"SELECT * FROM customer_accounts WHERE \"Id\" = {ownerId} FOR SHARE")
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(ct);
        if (account is null || account.IsDeleted || account.Status != CustomerAccountStatusType.Active)
        {
            throw Unavailable();
        }

        var member = await db.StorefrontMemberships.FromSqlInterpolated($"SELECT * FROM storefront_memberships WHERE customer_account_id = {ownerId} AND storefront_id = {storefrontId} FOR SHARE")
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(ct);
        if (member is null || member.IsDeleted || member.Status != StorefrontMembershipStatusType.Active ||
            member.LegacyCustomerProfileId is not { } profileId)
        {
            throw Unavailable();
        }

        var profile = await db.CustomerProfiles.FromSqlInterpolated($"SELECT * FROM customer_profiles WHERE \"Id\" = {profileId} FOR SHARE")
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(ct);
        if (profile is null || profile.IsDeleted || profile.CustomerAccountId != ownerId ||
            profile.StorefrontId != storefrontId || profile.AccountReconciliationStatus != CustomerAccountReconciliationStatusType.Linked ||
            profile.AccountStatus.Status != AccountStatusType.Active)
        {
            throw Unavailable();
        }

        return profileId;
    }

    private async Task<CustomerAddressEntity> RequireAddressAsync(Guid ownerId, Guid storefrontId, Guid addressId, CancellationToken ct)
    {
        var profileId = await RequireProfileAsync(ownerId, storefrontId, ct);
        var address = await db.CustomerAddresses.FromSqlInterpolated($"SELECT * FROM customer_addresses WHERE \"Id\" = {addressId} FOR SHARE")
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(ct);
        return address is not null && !address.IsDeleted && address.CustomerId == profileId ? address : throw Unavailable();
    }

    private async Task<(DeliveryContactRevisionEntity Contact, CustomerAddressEntity Address)> RequireContactAsync(
        DeliveryContactAuthority authority, Guid ownerId, Guid storefrontId, CancellationToken ct)
    {
        authority.Validate();
        if (authority.Authority != Authority)
        {
            throw Unavailable();
        }

        var contact = await db.Set<DeliveryContactRevisionEntity>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == authority.ResourceId && row.OwnerAccountId == ownerId && row.StorefrontId == storefrontId, ct);
        if (contact is null || contact.Revision != authority.Revision || contact.Digest != authority.Digest)
        {
            throw Unavailable();
        }

        var address = await RequireAddressAsync(ownerId, storefrontId, contact.AddressId, ct);
        var latest = await db.Set<DeliveryContactRevisionEntity>().IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.OwnerAccountId == ownerId && row.StorefrontId == storefrontId && row.AddressId == contact.AddressId)
            .MaxAsync(row => row.Revision, ct);
        if (latest != contact.Revision || !contact.MatchesCurrent(ownerId, storefrontId, address))
        {
            throw Unavailable();
        }

        return (contact, address);
    }

    private static DeliveryContactFailureException Unavailable() => new("CONTACT_UNAVAILABLE");
    private static DeliveryContactFailureException Conflict() => new("OPERATION_CONFLICT");
    private static DeliveryContactAuthority Ref(DeliveryContactRevisionEntity row) => new(Authority, row.Id, row.Revision, row.Digest);
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % 10));
    }
}
