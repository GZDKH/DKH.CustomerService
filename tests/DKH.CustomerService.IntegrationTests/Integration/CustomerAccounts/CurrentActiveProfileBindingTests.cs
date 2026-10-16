using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.IntegrationTests.Integration.CustomerAccounts;

[Trait("Category", "Unit")]
public sealed class CurrentActiveProfileBindingTests
{
    private const string Issuer = "https://profile.fixture.invalid/realms/test";
    private const string Subject = "opaque-human-subject";
    private static readonly DateTime VerifiedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReturnsActualLinkedProfileRatherThanAccountMembershipOrSubjectAsync()
    {
        await using var db = CreateContext();
        var account = CustomerAccountEntity.Create(Issuer, Subject, "synthetic@example.invalid", null, null, "en", VerifiedAt);
        var storefront = Guid.NewGuid();
        var profile = CustomerProfileEntity.Create(storefront, "deliberately-unrelated-legacy-user", "Synthetic");
        profile.BeginAccountReconciliation(VerifiedAt);
        profile.CompleteAccountReconciliation(account.Id, VerifiedAt);
        var membership = StorefrontMembershipEntity.Create(account.Id, storefront, VerifiedAt, profile.Id);
        db.AddRange(account, profile, membership);
        await db.SaveChangesAsync();

        var result = await new CurrentActiveProfileBinding(db, new CustomerJournalAccounts(db))
            .GetAsync(new(Issuer, Subject), storefront);

        Assert.Equal(profile.Id, result.CustomerProfileId);
        Assert.Equal(storefront, result.StorefrontId);
        Assert.NotEqual(account.Id, result.CustomerProfileId);
        Assert.NotEqual(membership.Id, result.CustomerProfileId);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("account-blocked")]
    [InlineData("account-deletion-pending")]
    [InlineData("account-deleted")]
    [InlineData("membership-blocked")]
    [InlineData("membership-revoked")]
    [InlineData("membership-deleted")]
    [InlineData("membership-no-profile")]
    [InlineData("membership-foreign-profile")]
    [InlineData("membership-duplicate")]
    [InlineData("profile-blocked")]
    [InlineData("profile-deleted")]
    [InlineData("profile-unlinked")]
    [InlineData("profile-foreign-account")]
    [InlineData("profile-foreign-storefront")]
    public async Task IneligibleCurrentLinksFailWithoutRepairAsync(string disposition)
    {
        await using var db = CreateContext();
        var seed = await SeedAsync(db);
        switch (disposition)
        {
            case "account-blocked":
                seed.Account.Block();
                break;
            case "account-deletion-pending":
                seed.Account.MarkDeletionPending();
                break;
            case "account-deleted":
                seed.Account.AnonymizeForDeletion(VerifiedAt);
                break;
            case "membership-blocked":
                seed.Membership.Block();
                break;
            case "membership-revoked":
                seed.Membership.Revoke();
                break;
            case "membership-deleted":
                seed.Membership.RevokeAndDelete();
                break;
            case "membership-no-profile":
                Set(seed.Membership, "LegacyCustomerProfileId", null);
                break;
            case "membership-foreign-profile":
                Set(seed.Membership, "LegacyCustomerProfileId", Guid.NewGuid());
                break;
            case "membership-duplicate":
                db.Add(StorefrontMembershipEntity.Create(seed.Account.Id, seed.Profile.StorefrontId, VerifiedAt, seed.Profile.Id));
                break;
            case "profile-blocked":
                seed.Profile.AccountStatus.Block("synthetic", "fixture");
                break;
            case "profile-deleted":
                seed.Profile.SoftDelete();
                break;
            case "profile-unlinked":
                Set(seed.Profile, "AccountReconciliationStatus", CustomerAccountReconciliationStatusType.PendingProof);
                break;
            case "profile-foreign-account":
                Set(seed.Profile, "CustomerAccountId", Guid.NewGuid());
                break;
            case "profile-foreign-storefront":
                Set(seed.Profile, "StorefrontId", Guid.NewGuid());
                break;
            default:
                throw new InvalidOperationException();
        }

        await db.SaveChangesAsync();
        var counts = (await db.CustomerAccounts.IgnoreQueryFilters().CountAsync(), await db.CustomerProfiles.IgnoreQueryFilters().CountAsync(),
            await db.StorefrontMemberships.IgnoreQueryFilters().CountAsync());
        var failure = await Record.ExceptionAsync(() => new CurrentActiveProfileBinding(db, new CustomerJournalAccounts(db))
            .GetAsync(new(Issuer, Subject), seed.Membership.StorefrontId));
        Assert.True(failure is CustomerAccountNotFoundException or CustomerAccountAccessException);
        Assert.Equal(counts, (await db.CustomerAccounts.IgnoreQueryFilters().CountAsync(), await db.CustomerProfiles.IgnoreQueryFilters().CountAsync(),
            await db.StorefrontMemberships.IgnoreQueryFilters().CountAsync()));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("verified")]
    [InlineData("future")]
    [InlineData("unverified")]
    [InlineData("deleted")]
    [InlineData("collision")]
    public async Task AliasRequiresCurrentUniqueVerifiedBindingAsync(string state)
    {
        await using var db = CreateContext();
        var seed = await SeedAsync(db);
        const string aliasSubject = "opaque:verified-alias";
        var alias = seed.Account.LinkIdentity(Issuer, aliasSubject, "oidc", null,
            state == "future" ? DateTime.UtcNow.AddDays(1) : state == "unverified" ? DateTime.UnixEpoch : VerifiedAt);
        if (state == "deleted")
        {
            alias.AnonymizeForAccountDeletion();
        }

        if (state == "collision")
        {
            db.Add(CustomerAccountEntity.Create(Issuer, aliasSubject, "other@example.invalid", null, null, "en", VerifiedAt));
        }

        await db.SaveChangesAsync();
        var query = new CurrentActiveProfileBinding(db, new CustomerJournalAccounts(db));
        if (state == "verified")
        {
            var result = await query.GetAsync(new(Issuer, aliasSubject), seed.Profile.StorefrontId);
            Assert.Equal(seed.Profile.Id, result.CustomerProfileId);
            Assert.NotEqual(alias.Id, result.CustomerProfileId);
        }
        else
        {
            await Assert.ThrowsAsync<CustomerAccountNotFoundException>(() => query.GetAsync(new(Issuer, aliasSubject), seed.Profile.StorefrontId));
        }
    }

    [Theory]
    [InlineData("account")]
    [InlineData("membership")]
    [InlineData("profile")]
    [InlineData("alias")]
    public async Task SecondReadUsesCurrentRowsDespitePreviouslyTrackedOwnerAsync(string revoked)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new TestContext(options);
        var seed = await SeedAsync(db);
        const string aliasSubject = "linked-opaque-human";
        seed.Account.LinkIdentity(Issuer, aliasSubject, "oidc", null, VerifiedAt);
        await db.SaveChangesAsync();
        var query = new CurrentActiveProfileBinding(db, new CustomerJournalAccounts(db));
        Assert.Equal(seed.Profile.Id, (await query.GetAsync(new(Issuer, aliasSubject), seed.Profile.StorefrontId)).CustomerProfileId);
        await using (var writer = new TestContext(options))
        {
            if (revoked == "account")
            {
                (await writer.CustomerAccounts.SingleAsync()).Block();
            }

            if (revoked == "membership")
            {
                (await writer.StorefrontMemberships.SingleAsync()).Revoke();
            }

            if (revoked == "profile")
            {
                (await writer.CustomerProfiles.SingleAsync()).SoftDelete();
            }

            if (revoked == "alias")
            {
                (await writer.LinkedCustomerIdentities.SingleAsync()).AnonymizeForAccountDeletion();
            }

            await writer.SaveChangesAsync();
        }

        var failure = await Record.ExceptionAsync(() => query.GetAsync(new(Issuer, aliasSubject), seed.Profile.StorefrontId));
        Assert.True(failure is CustomerAccountNotFoundException or CustomerAccountAccessException);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task MissingForeignOrEmptySelectionDoesNotProvisionAnythingAsync()
    {
        await using var db = CreateContext();
        var query = new CurrentActiveProfileBinding(db, new CustomerJournalAccounts(db));
        await Assert.ThrowsAsync<CustomerAccountNotFoundException>(() => query.GetAsync(new(Issuer, Subject), Guid.NewGuid()));
        var seed = await SeedAsync(db);
        await Assert.ThrowsAsync<CustomerAccountNotFoundException>(() => query.GetAsync(new(Issuer, Subject), Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => query.GetAsync(new(Issuer, Subject), Guid.Empty));
        Assert.Single(await db.CustomerAccounts.ToArrayAsync());
        Assert.Single(await db.CustomerProfiles.ToArrayAsync());
        Assert.Single(await db.StorefrontMemberships.ToArrayAsync());
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private static async Task<(CustomerAccountEntity Account, CustomerProfileEntity Profile, StorefrontMembershipEntity Membership)> SeedAsync(TestContext db)
    {
        var account = CustomerAccountEntity.Create(Issuer, Subject, "synthetic@example.invalid", null, null, "en", VerifiedAt);
        var profile = CustomerProfileEntity.Create(Guid.NewGuid(), "legacy-not-the-subject", "Synthetic");
        profile.BeginAccountReconciliation(VerifiedAt);
        profile.CompleteAccountReconciliation(account.Id, VerifiedAt);
        var membership = StorefrontMembershipEntity.Create(account.Id, profile.StorefrontId, VerifiedAt, profile.Id);
        db.AddRange(account, profile, membership);
        await db.SaveChangesAsync();
        return (account, profile, membership);
    }

    private static void Set(object entity, string property, object? value)
        => entity.GetType().GetProperty(property)!.SetValue(entity, value);

    private static TestContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class TestContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override Guid? GetCurrentUserId() => null;
    }
}
