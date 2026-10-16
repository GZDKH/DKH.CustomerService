using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.IntegrationTests.Integration.Journal;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.IntegrationTests.Integration.CustomerAccounts;

// Ordinary CI reuses the existing isolated synthetic PostgreSQL lifecycle.
// Local compilation/discovery is not a claim of PostgreSQL execution.
[Trait("Category", "Integration")]
public sealed class CurrentActiveProfileBindingPersistenceTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    [Fact]
    public async Task ActualRelationalProjectionResolvesPrimaryAndVerifiedAliasWithoutMutationAsync()
    {
        var seed = await SeedAsync();
        var alias = "profile-alias:" + Guid.NewGuid().ToString("N");
        await fixture.ChangeAsync(db =>
        {
            var account = db.CustomerAccounts.Single(row => row.Id == seed.AccountId);
            account.LinkIdentity(JournalHostFixture.Issuer, alias, "oidc", null, DateTime.UtcNow.AddMinutes(-1));
        });
        var before = await CountsAsync(seed.AccountId);
        var primary = await ReadAsync(seed.Identity, seed.Storefront);
        var linked = await ReadAsync(new(JournalHostFixture.Issuer, alias), seed.Storefront);
        Assert.Equal(seed.Profile, primary.CustomerProfileId);
        Assert.Equal(seed.Storefront, primary.StorefrontId);
        Assert.Equal(primary, linked);
        Assert.NotEqual(seed.AccountId, primary.CustomerProfileId);
        Assert.NotEqual(seed.Membership, primary.CustomerProfileId);
        Assert.Equal(before, await CountsAsync(seed.AccountId));
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("profile")]
    [InlineData("account")]
    public async Task RelationalReadRejectsRevocationDespitePreviouslyTrackedAccountAsync(string revoked)
    {
        var seed = await SeedAsync();
        await fixture.ReadAsync(async db =>
        {
            var query = new CurrentActiveProfileBinding(db, new CustomerJournalAccounts(db));
            Assert.Equal(seed.Profile, (await query.GetAsync(seed.Identity, seed.Storefront)).CustomerProfileId);
            await fixture.ChangeAsync(writer =>
            {
                if (revoked == "membership")
                {
                    writer.StorefrontMemberships.Single(row => row.Id == seed.Membership).Revoke();
                }
                else if (revoked == "profile")
                {
                    writer.CustomerProfiles.Single(row => row.Id == seed.Profile).SoftDelete();
                }
                else
                {
                    writer.CustomerAccounts.Single(row => row.Id == seed.AccountId).Block();
                }
            });
            var failure = await Record.ExceptionAsync(() => query.GetAsync(seed.Identity, seed.Storefront));
            Assert.True(failure is CustomerAccountNotFoundException or CustomerAccountAccessException);
            Assert.False(db.ChangeTracker.HasChanges());
            return true;
        });
    }

    [Fact]
    public async Task RelationalReadRejectsForeignOrAbsentProfileInsteadOfProvisioningAsync()
    {
        var seed = await SeedAsync();
        await Assert.ThrowsAsync<CustomerAccountNotFoundException>(() => ReadAsync(seed.Identity, Guid.NewGuid()));
        await fixture.ChangeAsync(db => db.StorefrontMemberships.Single(row => row.Id == seed.Membership).Revoke());
        var counts = await CountsAsync(seed.AccountId);
        await Assert.ThrowsAsync<CustomerAccountNotFoundException>(() => ReadAsync(seed.Identity, seed.Storefront));
        Assert.Equal(counts, await CountsAsync(seed.AccountId));
    }

    private Task<ActiveProfileBinding> ReadAsync(CustomerAccountIdentity identity, Guid storefront)
        => fixture.ReadAsync(db => new CurrentActiveProfileBinding(db, new CustomerJournalAccounts(db)).GetAsync(identity, storefront));

    private Task<string> CountsAsync(Guid accountId) => fixture.ReadAsync(async db =>
        $"{await db.CustomerAccounts.IgnoreQueryFilters().CountAsync(row => row.Id == accountId)}:" +
        $"{await db.CustomerProfiles.IgnoreQueryFilters().CountAsync(row => row.CustomerAccountId == accountId)}:" +
        $"{await db.StorefrontMemberships.IgnoreQueryFilters().CountAsync(row => row.CustomerAccountId == accountId)}:" +
        $"{await db.LinkedCustomerIdentities.IgnoreQueryFilters().CountAsync(row => row.CustomerAccountId == accountId)}");

    private async Task<Seed> SeedAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var now = DateTime.UtcNow.AddMinutes(-1);
        var profile = CustomerProfileEntity.Create(Guid.NewGuid(), "synthetic-legacy:" + Guid.NewGuid().ToString("N"), "Synthetic");
        profile.BeginAccountReconciliation(now);
        profile.CompleteAccountReconciliation(account.Id, now);
        var membership = StorefrontMembershipEntity.Create(account.Id, profile.StorefrontId, now, profile.Id);
        await fixture.ChangeAsync(db => db.AddRange(profile, membership));
        return new(account.Id, profile.Id, membership.Id, profile.StorefrontId, new(JournalHostFixture.Issuer, account.IdentitySubject));
    }

    private sealed record Seed(Guid AccountId, Guid Profile, Guid Membership, Guid Storefront, CustomerAccountIdentity Identity);
}
