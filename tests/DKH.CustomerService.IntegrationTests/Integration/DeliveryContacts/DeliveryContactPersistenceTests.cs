using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Infrastructure.DeliveryContacts;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.CustomerService.IntegrationTests.Integration.Journal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

// Reuses the existing disposable PostgreSQL lifecycle without changing journal
// source, RPCs or fixtures. These tests are compiled locally and run by normal CI.
[Trait("Category", "Integration")]
public sealed class DeliveryContactPersistenceTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ActualOwnerProfileLinkSealsCurrentObservationAndReplayKeepsOriginalVerificationAsync()
    {
        var seed = await SeedAsync();
        var operation = Guid.NewGuid();
        var contact = await ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, operation, Digest, default));
        contact.OwnerProfileId.Should().Be(seed.Address.CustomerId);
        contact.OwnerProfileId.Should().NotBe(seed.AccountId);
        var replay = await ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, operation, Digest, default));
        replay.Should().Be(contact);
        var accessOperation = Guid.NewGuid();
        var plan = Guid.NewGuid();
        var leg = Guid.NewGuid();
        var access = await ActAsync(seed.Actor, store => store.ResolveLiabilityAsync(seed.Actor, contact.Authority, plan, leg, accessOperation, null, Digest, default));
        var revalidated = await ActAsync(seed.Actor, store => store.ResolveLiabilityAsync(seed.Actor, contact.Authority, plan, leg, accessOperation, access.Id, Digest, default));
        revalidated.Should().Be(access);
        (await fixture.ReadAsync(db => db.Set<DeliveryContactLeaseEntity>().CountAsync(row => row.ActorAccountId == seed.AccountId))).Should().Be(1);
    }

    [Fact]
    public async Task WrongOwnerAndWrongStorefrontCannotSealAnotherAddressAsync()
    {
        var first = await SeedAsync();
        var other = await SeedAsync();
        var wrongOwner = () => ActAsync(other.Actor, store => store.SealAsync(other.Actor, first.Address.Id, Guid.NewGuid(), Digest, default));
        (await wrongOwner.Should().ThrowAsync<DeliveryContactFailureException>()).Which.Code.Should().Be("CONTACT_UNAVAILABLE");
        var wrongContext = first.Actor with { StorefrontId = Guid.NewGuid() };
        var wrongStorefront = () => ActAsync(wrongContext, store => store.SealAsync(wrongContext, first.Address.Id, Guid.NewGuid(), Digest, default));
        await wrongStorefront.Should().ThrowAsync<DeliveryContactFailureException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedOrDeletedSourceRefusesReplayAndRetainsImmutableMetadataAsync(bool purge)
    {
        var seed = await SeedAsync();
        var operation = Guid.NewGuid();
        var contact = await ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, operation, Digest, default));
        await fixture.ChangeAsync(db =>
        {
            var address = db.CustomerAddresses.Single(row => row.Id == seed.Address.Id);
            if (purge)
            {
                address.Anonymize();
            }
            else
            {
                address.Update(city: "Changed");
            }
        });
        var replay = () => ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, operation, Digest, default));
        await replay.Should().ThrowAsync<DeliveryContactFailureException>();
        var retained = await fixture.ReadAsync(db => db.Set<DeliveryContactRevisionEntity>().AsNoTracking().SingleAsync(row => row.Id == contact.Authority.ResourceId));
        retained.Digest.Should().Be(contact.Authority.Digest);
        retained.Revision.Should().Be(1);
    }

    [Fact]
    public async Task ConsentCanBeRevokedAndMissingGenuineDeliveryBoundaryNeverDisclosesRecipientAsync()
    {
        var seed = await SeedAsync();
        var contact = await ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, Guid.NewGuid(), Digest, default));
        var scope = new DeliveryContactScope(Guid.NewGuid(), "opaque-allocation", Guid.NewGuid(), 1, Guid.NewGuid(),
            new DeliveryContactAuthority("delivery.duty.v2", Guid.NewGuid(), 1, Digest));
        var consent = await ActAsync(seed.Actor, store => store.ConsentAsync(seed.Actor, contact.Authority, scope, DateTimeOffset.UtcNow.AddMinutes(2), Guid.NewGuid(), Digest, default));
        var access = () => ActAsync(seed.Actor, store => store.ResolveRecipientAsync(seed.Actor, contact.Authority, consent.Id, scope, Guid.NewGuid(), null, Digest, default));
        (await access.Should().ThrowAsync<DeliveryContactFailureException>()).Which.Code.Should().Be("AUTHORITY_UNAVAILABLE");
        var revokeOperation = Guid.NewGuid();
        var revoked = await ActAsync(seed.Actor, store => store.RevokeAsync(seed.Actor, consent.Id, 1, revokeOperation, Digest, default));
        revoked.Revoked.Should().BeTrue();
        revoked.Revision.Should().Be(2);
        (await ActAsync(seed.Actor, store => store.RevokeAsync(seed.Actor, consent.Id, 1, revokeOperation, Digest, default))).Should().Be(revoked);
        (await fixture.ReadAsync(db => db.Set<DeliveryContactLeaseEntity>().CountAsync(row => row.ActorAccountId == seed.AccountId))).Should().Be(0);
    }

    [Fact]
    public async Task PersistedRevisionCannotBeMutatedAndPopulatedDownRefusesBeforeDdlAsync()
    {
        var seed = await SeedAsync();
        var contact = await ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, Guid.NewGuid(), Digest, default));
        await fixture.ReadAsync(async db =>
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE delivery_contact_revisions SET \"Revision\" = 2 WHERE \"Id\" = {contact.Authority.ResourceId}"));
            error.SqlState.Should().Be("55000");
            error.MessageText.Should().Be("Delivery contact authority metadata is immutable except explicit consent revocation.");
            var before = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var down = await Record.ExceptionAsync(() => db.GetService<IMigrator>().MigrateAsync(JournalHostFixture.CurrentJournalMigration));
            var postgres = down as PostgresException ?? (down is InvalidOperationException { InnerException: PostgresException inner } ? inner : null);
            postgres.Should().NotBeNull("only the direct provider or its immediate EF execution wrapper is accepted");
            postgres!.SqlState.Should().Be("55000");
            postgres.MessageText.Should().Be("Delivery contact authority history must be retained before downgrade.");
            (await db.Database.GetAppliedMigrationsAsync()).Should().Equal(before);
            (await db.Set<DeliveryContactRevisionEntity>().CountAsync(row => row.Id == contact.Authority.ResourceId)).Should().Be(1);
            return true;
        });
    }

    [Fact]
    public async Task RevocationOperationCannotBeReusedForAnotherConsentOrChangedBodyAsync()
    {
        var seed = await SeedAsync();
        var contact = await ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, Guid.NewGuid(), Digest, default));
        var scope = new DeliveryContactScope(Guid.NewGuid(), "first-allocation", Guid.NewGuid(), 1, Guid.NewGuid(),
            new DeliveryContactAuthority("delivery.duty.v2", Guid.NewGuid(), 1, Digest));
        var first = await ActAsync(seed.Actor, store => store.ConsentAsync(seed.Actor, contact.Authority, scope, DateTimeOffset.UtcNow.AddMinutes(2), Guid.NewGuid(), Digest, default));
        var second = await ActAsync(seed.Actor, store => store.ConsentAsync(seed.Actor, contact.Authority, scope with { AllocationPublicRef = "second-allocation" }, DateTimeOffset.UtcNow.AddMinutes(2), Guid.NewGuid(), Digest, default));
        var operation = Guid.NewGuid();
        var original = await ActAsync(seed.Actor, store => store.RevokeAsync(seed.Actor, first.Id, 1, operation, Digest, default));
        var changedTarget = () => ActAsync(seed.Actor, store => store.RevokeAsync(seed.Actor, second.Id, 1, operation, new string('b', 64), default));
        (await changedTarget.Should().ThrowAsync<DeliveryContactFailureException>()).Which.Code.Should().Be("OPERATION_CONFLICT");
        var changedBody = () => ActAsync(seed.Actor, store => store.RevokeAsync(seed.Actor, first.Id, 1, operation, new string('b', 64), default));
        (await changedBody.Should().ThrowAsync<DeliveryContactFailureException>()).Which.Code.Should().Be("OPERATION_CONFLICT");
        (await ActAsync(seed.Actor, store => store.RevokeAsync(seed.Actor, first.Id, 1, operation, Digest, default))).Should().Be(original);
        var untouched = await fixture.ReadAsync(db => db.Set<DeliveryContactConsentEntity>().AsNoTracking().SingleAsync(row => row.Id == second.Id));
        untouched.Revision.Should().Be(1);
        untouched.RevokedAtUtc.Should().BeNull();
        untouched.RevokeOperationId.Should().BeNull();
        await fixture.ReadAsync(async db =>
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE delivery_contact_consents SET \"Revision\" = 2, \"RevokedAtUtc\" = now(), \"RevokeOperationId\" = {operation}, \"RevokeRequestDigest\" = {new string('b', 64)} WHERE \"Id\" = {second.Id}"));
            error.SqlState.Should().Be("23505");
            error.ConstraintName.Should().Be("ux_delivery_contact_owner_revoke_operation");
            return true;
        });
    }

    [Theory]
    [InlineData("delivery_contact_revisions")]
    [InlineData("delivery_contact_consents")]
    [InlineData("delivery_contact_leases")]
    [InlineData("all")]
    public async Task StatementTruncateCannotEraseAnyRetainedHistoryAsync(string table)
    {
        var seed = await SeedAsync();
        var contact = await ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, Guid.NewGuid(), Digest, default));
        await fixture.ReadAsync(async db =>
        {
            var before = await db.Set<DeliveryContactRevisionEntity>().AsNoTracking().OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.Digest, row.Revision }).ToArrayAsync();
            // Closed theory literals only, never a caller-controlled identifier.
            var sql = table switch
            {
                "delivery_contact_revisions" => "TRUNCATE TABLE public.delivery_contact_revisions CASCADE",
                "delivery_contact_consents" => "TRUNCATE TABLE public.delivery_contact_consents CASCADE",
                "delivery_contact_leases" => "TRUNCATE TABLE public.delivery_contact_leases",
                "all" => "TRUNCATE TABLE public.delivery_contact_revisions, public.delivery_contact_consents, public.delivery_contact_leases",
                _ => throw new ArgumentException("Unsupported retained table."),
            };
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
            error.SqlState.Should().Be("55000");
            error.MessageText.Should().Be("Delivery contact authority metadata is immutable except explicit consent revocation.");
            (await db.Set<DeliveryContactRevisionEntity>().AsNoTracking().OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.Digest, row.Revision }).ToArrayAsync()).Should().Equal(before);
            (await db.Set<DeliveryContactRevisionEntity>().CountAsync(row => row.Id == contact.Authority.ResourceId)).Should().Be(1);
            return true;
        });
    }

    [Fact]
    public async Task RollbackAfterSaveDoesNotLeaveContactOrOperationReceiptAsync()
    {
        var seed = await SeedAsync();
        var operation = Guid.NewGuid();
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await Store(db).SealAsync(seed.Actor, seed.Address.Id, operation, Digest, default);
            await transaction.RollbackAsync();
            return true;
        });
        (await fixture.ReadAsync(db => db.Set<DeliveryContactRevisionEntity>().CountAsync(row => row.OperationId == operation))).Should().Be(0);
    }

    private async Task<(DeliveryContactActor Actor, Guid AccountId, CustomerAddressEntity Address)> SeedAsync()
    {
        await fixture.ReadAsync(async db =>
        {
            await db.Database.MigrateAsync();
            return true;
        });
        var account = await fixture.SeedAccountAsync();
        var storefront = Guid.NewGuid();
        var profile = CustomerProfileEntity.Create(storefront, "legacy:" + Guid.NewGuid().ToString("N"), "Recipient");
        profile.BeginAccountReconciliation(DateTime.UtcNow);
        profile.CompleteAccountReconciliation(account.Id, DateTime.UtcNow);
        var address = CustomerAddressEntity.Create(profile.Id, "Home", "GB", "London", firstName: "Recipient", street: "Street");
        await fixture.ChangeAsync(db =>
        {
            db.CustomerProfiles.Add(profile);
            db.StorefrontMemberships.Add(StorefrontMembershipEntity.Create(account.Id, storefront, DateTime.UtcNow, profile.Id));
            db.CustomerAddresses.Add(address);
        });
        return (new(new CustomerAccountIdentity(JournalHostFixture.Issuer, account.IdentitySubject), storefront), account.Id, address);
    }

    private Task<T> ActAsync<T>(DeliveryContactActor actor, Func<DeliveryContactStore, Task<T>> action)
    {
        _ = actor;
        return fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var result = await action(Store(db));
            await transaction.CommitAsync();
            return result;
        });
    }

    private static DeliveryContactStore Store(AppDbContext db) => new(db, new UnavailableDeliveryContactScopeAuthorizer(), TimeProvider.System);
}
