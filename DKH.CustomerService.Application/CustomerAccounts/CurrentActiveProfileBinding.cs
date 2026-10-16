using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Enums;

namespace DKH.CustomerService.Application.CustomerAccounts;

/// <summary>Reads existing verified linkage without provisioning or reconciliation.</summary>
public sealed class CurrentActiveProfileBinding(IAppDbContext db, CustomerJournalAccounts accounts)
{
    public async Task<ActiveProfileBinding> GetAsync(CustomerAccountIdentity identity, Guid storefrontId,
        CancellationToken cancellationToken = default)
    {
        if (storefrontId == Guid.Empty)
        {
            throw new ArgumentException("A current storefront is required.", nameof(storefrontId));
        }

        var owner = await accounts.ResolveAsync(identity, cancellationToken);
        var issuer = CustomerAccountEntity.NormalizeIdentityIssuer(identity.Issuer);
        var subject = CustomerAccountEntity.NormalizeIdentitySubject(identity.Subject);
        var now = DateTime.UtcNow;
        var aliases = db.LinkedCustomerIdentities.IgnoreQueryFilters().AsNoTracking();
        var accountRows = db.CustomerAccounts.IgnoreQueryFilters().AsNoTracking();
        // Recheck the complete identity candidate set in the final database read.
        // The earlier resolver or tracked entities cannot keep a revoked alias alive.
        var identityCandidates = accountRows.Where(account =>
            account.IdentityIssuer == issuer && account.IdentitySubject == subject ||
            aliases.Any(alias => !alias.IsDeleted && alias.CustomerAccountId == account.Id &&
                alias.ProviderAuthority == issuer && alias.ProviderSubject == subject &&
                alias.VerifiedAt > DateTime.UnixEpoch && alias.VerifiedAt <= now));
        var memberships = db.StorefrontMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(membership => !membership.IsDeleted && membership.CustomerAccountId == owner.AccountId &&
                membership.StorefrontId == storefrontId && membership.Status == StorefrontMembershipStatusType.Active);
        var profiles = db.CustomerProfiles.IgnoreQueryFilters().AsNoTracking();
        var result = await (
            from membership in memberships
            join profile in profiles on membership.LegacyCustomerProfileId equals profile.Id
            where memberships.Count() == 1 && identityCandidates.Count() == 1 &&
                identityCandidates.Any(account => account.Id == owner.AccountId && !account.IsDeleted &&
                    account.Status == CustomerAccountStatusType.Active) &&
                !profile.IsDeleted && profile.CustomerAccountId == owner.AccountId && profile.StorefrontId == storefrontId &&
                profile.AccountReconciliationStatus == CustomerAccountReconciliationStatusType.Linked &&
                profile.AccountStatus.Status == AccountStatusType.Active
            select new ActiveProfileBinding(profile.Id, profile.StorefrontId))
            .Take(2).ToArrayAsync(cancellationToken);
        return result.Length == 1 ? result[0] : throw CustomerJournalAccounts.NotFound();
    }
}
