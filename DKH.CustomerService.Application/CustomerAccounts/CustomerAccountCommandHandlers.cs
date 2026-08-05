using System.Diagnostics;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Application.Observability;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Domain.Enums;

namespace DKH.CustomerService.Application.CustomerAccounts;

public sealed class EnsureCustomerAccountCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<EnsureCustomerAccountCommand, CustomerAccountModel>
{
    public async Task<CustomerAccountModel> Handle(
        EnsureCustomerAccountCommand request,
        CancellationToken cancellationToken)
    {
        var issuer = CustomerAccountEntity.NormalizeIdentityIssuer(request.Identity.Issuer);
        var subject = CustomerAccountEntity.NormalizeIdentitySubject(request.Identity.Subject);
        var account = await dbContext.CustomerAccounts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                candidate => candidate.IdentityIssuer == issuer && candidate.IdentitySubject == subject,
                cancellationToken);

        if (account is not null)
        {
            EnsureAccountCanAuthenticate(account);
            if (request.Identity.EmailVerifiedAt >= account.EmailVerifiedAt &&
                !string.Equals(account.VerifiedEmail, request.Identity.VerifiedEmail, StringComparison.OrdinalIgnoreCase))
            {
                account.UpdateVerifiedEmail(request.Identity.VerifiedEmail, request.Identity.EmailVerifiedAt);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return account.ToContractModel();
        }

        account = CustomerAccountEntity.Create(
            issuer,
            subject,
            request.Identity.VerifiedEmail,
            request.Identity.FirstName,
            request.Identity.LastName,
            request.Identity.PreferredLocale,
            request.Identity.EmailVerifiedAt);
        dbContext.CustomerAccounts.Add(account);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return account.ToContractModel();
        }
        catch (DbUpdateException)
        {
            dbContext.ClearTrackedChanges();
            account = await dbContext.CustomerAccounts
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    candidate => candidate.IdentityIssuer == issuer && candidate.IdentitySubject == subject,
                    cancellationToken)
                ?? throw new CustomerAccountConflictException("Customer account creation conflicted with another request.");
            EnsureAccountCanAuthenticate(account);
            return account.ToContractModel();
        }
    }

    internal static void EnsureAccountCanAuthenticate(CustomerAccountEntity account)
    {
        if (account.IsDeleted || account.Status == CustomerAccountStatusType.DeletionPending)
        {
            throw new CustomerAccountAccessException("Customer account is pending deletion.");
        }

        if (account.Status == CustomerAccountStatusType.Blocked)
        {
            throw new CustomerAccountAccessException("Customer account is blocked.");
        }
    }
}

public sealed class UpdateCustomerAccountCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<UpdateCustomerAccountCommand, CustomerAccountModel>
{
    public async Task<CustomerAccountModel> Handle(
        UpdateCustomerAccountCommand request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountHandlerSupport.RequireAccountAsync(
            dbContext,
            request.Identity,
            includeLinkedIdentities: false,
            cancellationToken);
        EnsureCustomerAccountCommandHandler.EnsureAccountCanAuthenticate(account);

        account.UpdateProfile(
            request.FirstName ?? account.FirstName,
            request.LastName ?? account.LastName,
            request.PreferredLocale ?? account.PreferredLocale);
        await dbContext.SaveChangesAsync(cancellationToken);
        return account.ToContractModel();
    }
}

public sealed class EnsureStorefrontMembershipCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<EnsureStorefrontMembershipCommand, StorefrontMembershipModel>
{
    public async Task<StorefrontMembershipModel> Handle(
        EnsureStorefrontMembershipCommand request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        if (request.StorefrontId == Guid.Empty)
        {
            throw new CustomerAccountConflictException("A resolved storefront is required.");
        }

        var account = await CustomerAccountHandlerSupport.RequireAccountAsync(
            dbContext,
            request.Identity,
            includeLinkedIdentities: true,
            cancellationToken);
        EnsureCustomerAccountCommandHandler.EnsureAccountCanAuthenticate(account);

        var membership = await dbContext.StorefrontMemberships
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerAccountId == account.Id &&
                             candidate.StorefrontId == request.StorefrontId,
                cancellationToken);

        if (membership is not null)
        {
            if (membership.IsDeleted || membership.Status == StorefrontMembershipStatusType.Revoked)
            {
                throw new CustomerAccountAccessException("Storefront membership is revoked.");
            }

            if (membership.Status == StorefrontMembershipStatusType.Blocked)
            {
                throw new CustomerAccountAccessException("Storefront membership is blocked.");
            }

            membership.RegisterAuthenticatedTouch(request.AuthenticatedAt);
            await ReconcileLegacyProfileAsync(account, membership, request, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            CustomerAccountMetrics.RecordMembership(
                StorefrontMembershipOutcome.ReturningTouch,
                request.StorefrontId,
                Stopwatch.GetElapsedTime(startedAt));
            return membership.ToContractModel();
        }

        var legacyProfile = await FindLegacyProfileAsync(request, cancellationToken);
        await ValidateLegacyProfileAsync(account, legacyProfile, request.AuthenticatedAt, cancellationToken);

        membership = StorefrontMembershipEntity.Create(
            account.Id,
            request.StorefrontId,
            request.AuthenticatedAt,
            legacyProfile?.Id);
        dbContext.StorefrontMemberships.Add(membership);

        if (legacyProfile is not null)
        {
            ReconcileLegacyProfile(account, legacyProfile, request.AuthenticatedAt);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            CustomerAccountMetrics.RecordMembership(
                StorefrontMembershipOutcome.FirstTouch,
                request.StorefrontId,
                Stopwatch.GetElapsedTime(startedAt));
            return membership.ToContractModel();
        }
        catch (DbUpdateException)
        {
            dbContext.ClearTrackedChanges();
            membership = await dbContext.StorefrontMemberships
                .SingleOrDefaultAsync(
                    candidate => candidate.CustomerAccountId == account.Id &&
                                 candidate.StorefrontId == request.StorefrontId,
                    cancellationToken)
                ?? throw new CustomerAccountConflictException("Storefront membership creation conflicted with another request.");
            CustomerAccountMetrics.RecordMembership(
                StorefrontMembershipOutcome.ReturningTouch,
                request.StorefrontId,
                Stopwatch.GetElapsedTime(startedAt));
            return membership.ToContractModel();
        }
    }

    private async Task<CustomerProfileEntity?> FindLegacyProfileAsync(
        EnsureStorefrontMembershipCommand request,
        CancellationToken cancellationToken)
    {
        var subject = CustomerAccountEntity.NormalizeIdentitySubject(request.Identity.Subject);
        return await dbContext.CustomerProfiles
            .Include(profile => profile.ExternalIdentities)
            .SingleOrDefaultAsync(
                profile => profile.StorefrontId == request.StorefrontId && profile.UserId == subject,
                cancellationToken);
    }

    private async Task ReconcileLegacyProfileAsync(
        CustomerAccountEntity account,
        StorefrontMembershipEntity membership,
        EnsureStorefrontMembershipCommand request,
        CancellationToken cancellationToken)
    {
        if (!membership.LegacyCustomerProfileId.HasValue)
        {
            return;
        }

        var legacyProfile = await dbContext.CustomerProfiles
            .Include(profile => profile.ExternalIdentities)
            .SingleOrDefaultAsync(
                profile => profile.Id == membership.LegacyCustomerProfileId.Value,
                cancellationToken);
        if (legacyProfile is null ||
            legacyProfile.AccountReconciliationStatus == CustomerAccountReconciliationStatusType.Linked)
        {
            return;
        }

        await ValidateLegacyProfileAsync(account, legacyProfile, request.AuthenticatedAt, cancellationToken);
        ReconcileLegacyProfile(account, legacyProfile, request.AuthenticatedAt);
    }

    private async Task ValidateLegacyProfileAsync(
        CustomerAccountEntity account,
        CustomerProfileEntity? legacyProfile,
        DateTime attemptedAt,
        CancellationToken cancellationToken)
    {
        if (legacyProfile is null)
        {
            return;
        }

        if (legacyProfile.AccountReconciliationStatus == CustomerAccountReconciliationStatusType.Quarantined)
        {
            throw new CustomerAccountConflictException("Legacy profile requires reconciliation review.");
        }

        if (legacyProfile.CustomerAccountId.HasValue && legacyProfile.CustomerAccountId != account.Id)
        {
            legacyProfile.BeginAccountReconciliation(attemptedAt);
            legacyProfile.QuarantineAccountReconciliation("account_subject_conflict", attemptedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new CustomerAccountConflictException("Legacy profile is linked to another account.");
        }

        try
        {
            foreach (var legacyIdentity in legacyProfile.ExternalIdentities.Where(identity => !identity.IsDeleted))
            {
                var authority = CustomerAccountEntity.NormalizeLinkedProviderAuthority(legacyIdentity.Provider);
                var subject = CustomerAccountEntity.NormalizeIdentitySubject(legacyIdentity.ProviderUserId);
                _ = CustomerAccountEntity.NormalizeLinkedProviderKind(legacyIdentity.Provider);
                var owner = await dbContext.LinkedCustomerIdentities
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(
                        identity => identity.ProviderAuthority == authority && identity.ProviderSubject == subject,
                        cancellationToken);

                if (owner is not null && owner.CustomerAccountId != account.Id)
                {
                    legacyProfile.BeginAccountReconciliation(attemptedAt);
                    legacyProfile.QuarantineAccountReconciliation("external_identity_conflict", attemptedAt);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    throw new CustomerAccountConflictException("Legacy provider identity is linked to another account.");
                }
            }
        }
        catch (ArgumentException)
        {
            legacyProfile.BeginAccountReconciliation(attemptedAt);
            legacyProfile.QuarantineAccountReconciliation("invalid_external_identity", attemptedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new CustomerAccountConflictException("Legacy provider identity requires reconciliation review.");
        }
    }

    private static void ReconcileLegacyProfile(
        CustomerAccountEntity account,
        CustomerProfileEntity legacyProfile,
        DateTime attemptedAt)
    {
        legacyProfile.BeginAccountReconciliation(attemptedAt);

        foreach (var legacyIdentity in legacyProfile.ExternalIdentities.Where(identity => !identity.IsDeleted))
        {
            var authority = CustomerAccountEntity.NormalizeLinkedProviderAuthority(legacyIdentity.Provider);
            var subject = CustomerAccountEntity.NormalizeIdentitySubject(legacyIdentity.ProviderUserId);
            if (account.LinkedIdentities.Any(identity =>
                    identity.ProviderAuthority == authority && identity.ProviderSubject == subject))
            {
                continue;
            }

            account.LinkIdentity(
                authority,
                subject,
                legacyIdentity.Provider,
                legacyIdentity.DisplayName,
                legacyIdentity.LinkedAt,
                legacyIdentity.Id);
        }

        legacyProfile.CompleteAccountReconciliation(account.Id, attemptedAt);
    }
}

public sealed class DeleteStorefrontMembershipCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<DeleteStorefrontMembershipCommand>
{
    public async Task Handle(DeleteStorefrontMembershipCommand request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var account = await CustomerAccountHandlerSupport.RequireAccountAsync(
            dbContext,
            request.Identity,
            includeLinkedIdentities: false,
            cancellationToken);
        var membership = await dbContext.StorefrontMemberships
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerAccountId == account.Id &&
                             candidate.StorefrontId == request.StorefrontId,
                cancellationToken)
            ?? throw new CustomerAccountNotFoundException("Storefront membership was not found.");

        if (membership.LegacyCustomerProfileId.HasValue)
        {
            var profile = await dbContext.CustomerProfiles
                .IgnoreQueryFilters()
                .Include(candidate => candidate.Addresses)
                .Include(candidate => candidate.WishlistItems)
                .Include(candidate => candidate.ExternalIdentities)
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == membership.LegacyCustomerProfileId.Value,
                    cancellationToken);
            profile?.Anonymize();
        }

        membership.RevokeAndDelete();
        await dbContext.SaveChangesAsync(cancellationToken);
        CustomerAccountMetrics.RecordMembership(
            StorefrontMembershipOutcome.Revoked,
            request.StorefrontId,
            Stopwatch.GetElapsedTime(startedAt));
    }
}

public sealed class DeleteCustomerAccountDataCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<DeleteCustomerAccountDataCommand>
{
    public async Task Handle(DeleteCustomerAccountDataCommand request, CancellationToken cancellationToken)
    {
        var account = await CustomerAccountHandlerSupport.RequireAccountAsync(
            dbContext,
            request.Identity,
            includeLinkedIdentities: true,
            cancellationToken);
        var memberships = await dbContext.StorefrontMemberships
            .IgnoreQueryFilters()
            .Where(membership => membership.CustomerAccountId == account.Id)
            .ToListAsync(cancellationToken);
        var profileIds = memberships
            .Where(membership => membership.LegacyCustomerProfileId.HasValue)
            .Select(membership => membership.LegacyCustomerProfileId!.Value)
            .ToList();
        var profiles = await dbContext.CustomerProfiles
            .IgnoreQueryFilters()
            .Include(profile => profile.Addresses)
            .Include(profile => profile.WishlistItems)
            .Include(profile => profile.ExternalIdentities)
            .Where(profile => profile.CustomerAccountId == account.Id || profileIds.Contains(profile.Id))
            .ToListAsync(cancellationToken);
        var linkedIdentities = await dbContext.LinkedCustomerIdentities
            .IgnoreQueryFilters()
            .Where(identity => identity.CustomerAccountId == account.Id)
            .ToListAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            profile.Anonymize();
        }

        foreach (var membership in memberships)
        {
            membership.RevokeAndDelete();
        }

        foreach (var linkedIdentity in linkedIdentities)
        {
            linkedIdentity.AnonymizeForAccountDeletion();
        }

        account.AnonymizeForDeletion(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

internal static class CustomerAccountHandlerSupport
{
    public static async Task<CustomerAccountEntity> RequireAccountAsync(
        IAppDbContext dbContext,
        CustomerAccountIdentity identity,
        bool includeLinkedIdentities,
        CancellationToken cancellationToken)
    {
        var issuer = CustomerAccountEntity.NormalizeIdentityIssuer(identity.Issuer);
        var subject = CustomerAccountEntity.NormalizeIdentitySubject(identity.Subject);
        IQueryable<CustomerAccountEntity> query = dbContext.CustomerAccounts;
        if (includeLinkedIdentities)
        {
            query = query.Include(account => account.LinkedIdentities);
        }

        var account = await query.SingleOrDefaultAsync(
                          account => account.IdentityIssuer == issuer && account.IdentitySubject == subject,
                          cancellationToken)
                      ?? throw new CustomerAccountNotFoundException("Customer account was not found.");
        EnsureCustomerAccountCommandHandler.EnsureAccountCanAuthenticate(account);
        return account;
    }
}
