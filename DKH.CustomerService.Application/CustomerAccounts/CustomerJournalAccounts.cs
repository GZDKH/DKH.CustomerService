using DKH.CustomerService.Domain.Enums;

namespace DKH.CustomerService.Application.CustomerAccounts;

public sealed record CustomerJournalOwner(Guid AccountId);

/// <summary>Account-wide journal ownership and read-only, verified legacy linkage.</summary>
public sealed class CustomerJournalAccounts(IAppDbContext dbContext)
{
    public static void RequireActiveOwner(Domain.Entities.CustomerAccount.CustomerAccountEntity account)
    {
        EnsureCustomerAccountCommandHandler.EnsureAccountCanAuthenticate(account);
        if (account.Status != CustomerAccountStatusType.Active)
        {
            throw new CustomerAccountAccessException("Personal account is unavailable.");
        }
    }

    public async Task<CustomerJournalOwner> ResolveAsync(
        CustomerAccountIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var issuer = Domain.Entities.CustomerAccount.CustomerAccountEntity.NormalizeIdentityIssuer(identity.Issuer);
        var subject = Domain.Entities.CustomerAccount.CustomerAccountEntity.NormalizeIdentitySubject(identity.Subject);
        var now = DateTime.UtcNow;
        var accounts = dbContext.CustomerAccounts.IgnoreQueryFilters();
        var primary = accounts.Where(account => account.IdentityIssuer == issuer && account.IdentitySubject == subject);
        var linked = dbContext.LinkedCustomerIdentities.IgnoreQueryFilters()
            .Where(link => !link.IsDeleted && link.ProviderAuthority == issuer && link.ProviderSubject == subject &&
                link.VerifiedAt > DateTime.UnixEpoch && link.VerifiedAt <= now)
            .Join(accounts, link => link.CustomerAccountId, account => account.Id, (_, account) => account);
        // Evaluate primary and already-verified issuer/subject aliases together.
        // A collision between the two authorities is not an email merge or a
        // reason to choose whichever account happened to be queried first.
        var matches = await primary.Union(linked)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (matches.Count != 1)
        {
            throw NotFound();
        }

        EnsureCustomerAccountCommandHandler.EnsureAccountCanAuthenticate(matches[0]);
        return new CustomerJournalOwner(matches[0].Id);
    }

    public async Task<Guid> ResolveLegacyProfileAsync(
        CustomerAccountIdentity identity,
        Guid storefrontId,
        Guid legacyProfileId,
        CancellationToken cancellationToken = default)
    {
        var owner = await ResolveAsync(identity, cancellationToken);
        var memberships = dbContext.StorefrontMemberships.IgnoreQueryFilters();
        var profiles = await dbContext.CustomerProfiles.IgnoreQueryFilters()
            .Where(profile => profile.Id == legacyProfileId && !profile.IsDeleted &&
                profile.CustomerAccountId == owner.AccountId && profile.StorefrontId == storefrontId &&
                profile.AccountReconciliationStatus == CustomerAccountReconciliationStatusType.Linked &&
                profile.AccountStatus.Status == AccountStatusType.Active &&
                memberships.Any(membership => !membership.IsDeleted &&
                    membership.CustomerAccountId == owner.AccountId && membership.StorefrontId == storefrontId &&
                    membership.LegacyCustomerProfileId == profile.Id &&
                    membership.Status == StorefrontMembershipStatusType.Active))
            .Select(profile => profile.Id)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return profiles.Length == 1 ? profiles[0] : throw NotFound();
    }

    internal static CustomerAccountNotFoundException NotFound()
        => new("Personal resource was not found.");
}
