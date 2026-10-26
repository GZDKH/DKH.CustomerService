using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

public sealed partial class DeliveryContactPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedSelfOrCompanyActorUsesActualLockedProfileDistinctFromRecipientAsync(bool representedCompany)
    {
        var recipient = await SeedAsync();
        var actor = await SeedAsync(recipient.Actor.StorefrontId);
        actor.Address.CustomerId.Should().NotBe(recipient.Address.CustomerId).And.NotBe(actor.AccountId);
        var contact = await ActAsync(recipient.Actor, store => store.SealAsync(recipient.Actor, recipient.Address.Id, Guid.NewGuid(), Digest, default));
        var scope = Scope();
        var consent = await ActAsync(recipient.Actor, store => store.ConsentAsync(recipient.Actor, contact.Authority, scope,
            DateTimeOffset.UtcNow.AddMinutes(2), Guid.NewGuid(), Digest, default));
        var accepted = representedCompany ? new DeliveryContactActingActor(null, new("counterparty.party", Guid.NewGuid(), 4, Digest))
            : new DeliveryContactActingActor(actor.Address.CustomerId, null);
        var decision = new DeliveryContactScopeDecision(scope, recipient.Address.CustomerId, actor.Actor.StorefrontId,
            Guid.NewGuid(), false, DateTimeOffset.UtcNow.AddMinutes(2), consent.Id, contact.Authority.ResourceId, accepted);
        var owner = new SyntheticOwner(decision);
        var operation = Guid.NewGuid();
        var first = await ActAsync(actor.Actor, store => store.ResolveRecipientAsync(actor.Actor, contact.Authority, consent.Id,
            scope, operation, null, Digest, default), owner);
        var replay = await ActAsync(actor.Actor, store => store.ResolveRecipientAsync(actor.Actor, contact.Authority, consent.Id,
            scope, operation, first.Access.Id, Digest, default), owner);
        replay.Should().Be(first);
        first.RecipientName.Should().Be("Recipient");
        first.Access.ExpiresAtUtc.Should().BeOnOrBefore(consent.ValidUntilUtc);
        owner.Calls.Should().Be(2);
        await ActAsync(recipient.Actor, store => store.RevokeAsync(recipient.Actor, consent.Id, 1, Guid.NewGuid(), Digest, default));
        await Assert.ThrowsAsync<DeliveryContactFailureException>(() => ActAsync(actor.Actor, store => store.ResolveRecipientAsync(actor.Actor,
            contact.Authority, consent.Id, scope, operation, first.Access.Id, Digest, default), owner));
        (await fixture.ReadAsync(db => db.Set<DeliveryContactLeaseEntity>().CountAsync(row => row.ActorAccountId == actor.AccountId))).Should().Be(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("account-not-profile")]
    [InlineData("recipient-not-actor")]
    [InlineData("foreign-profile")]
    [InlineData("zero-profile")]
    [InlineData("ambiguous")]
    [InlineData("empty-actor")]
    [InlineData("wrong-company-authority")]
    [InlineData("malformed-company-digest")]
    public async Task NativeAcceptedActorMustBindActualCurrentProfileBeforeContactOrReceiptAsync(string changed)
    {
        var recipient = await SeedAsync();
        var actor = await SeedAsync(recipient.Actor.StorefrontId);
        var contact = await ActAsync(recipient.Actor, store => store.SealAsync(recipient.Actor, recipient.Address.Id, Guid.NewGuid(), Digest, default));
        var scope = Scope();
        var consent = await ActAsync(recipient.Actor, store => store.ConsentAsync(recipient.Actor, contact.Authority, scope,
            DateTimeOffset.UtcNow.AddMinutes(2), Guid.NewGuid(), Digest, default));
        DeliveryContactActingActor? accepted = changed switch
        {
            "missing" => null,
            "account-not-profile" => new(actor.AccountId, null),
            "recipient-not-actor" => new(recipient.Address.CustomerId, null),
            "foreign-profile" => new(Guid.NewGuid(), null),
            "zero-profile" => new(Guid.Empty, null),
            "ambiguous" => new(actor.Address.CustomerId, new("counterparty.party", Guid.NewGuid(), 1, Digest)),
            "empty-actor" => new(null, null),
            "wrong-company-authority" => new(null, new("customer.delivery-contact.v1", Guid.NewGuid(), 1, Digest)),
            "malformed-company-digest" => new(null, new("counterparty.party", Guid.NewGuid(), 1, new string('A', 64))),
            _ => throw new ArgumentException("Unknown test input."),
        };
        var decision = new DeliveryContactScopeDecision(scope, recipient.Address.CustomerId, actor.Actor.StorefrontId,
            Guid.NewGuid(), false, DateTimeOffset.UtcNow.AddMinutes(2), consent.Id, contact.Authority.ResourceId, accepted);
        var owner = new SyntheticOwner(decision);
        await Assert.ThrowsAsync<DeliveryContactFailureException>(() => ActAsync(actor.Actor, store => store.ResolveRecipientAsync(actor.Actor,
            contact.Authority, consent.Id, scope, Guid.NewGuid(), null, Digest, default), owner));
        owner.Calls.Should().Be(1);
        (await fixture.ReadAsync(db => db.Set<DeliveryContactLeaseEntity>().CountAsync(row => row.ActorAccountId == actor.AccountId))).Should().Be(0);
        (await fixture.ReadAsync(db => db.Set<DeliveryContactConsentEntity>().SingleAsync(row => row.Id == consent.Id))).Revision.Should().Be(1);
    }

    [Fact]
    public async Task NonRecursiveOwnerReadCompletesWhileOriginalAccountLockIsActuallyHeldAsync()
    {
        // Real Customer PostgreSQL and transaction; explicit synthetic Delivery
        // DATA port. Delivery separately proves its real callback makes ZERO
        // Customer calls. This case proves the original lock is not dropped.
        var seed = await SeedAsync();
        var contact = await ActAsync(seed.Actor, store => store.SealAsync(seed.Actor, seed.Address.Id, Guid.NewGuid(), Digest, default));
        var scope = Scope();
        var consent = await ActAsync(seed.Actor, store => store.ConsentAsync(seed.Actor, contact.Authority, scope,
            DateTimeOffset.UtcNow.AddMinutes(2), Guid.NewGuid(), Digest, default));
        var decision = new DeliveryContactScopeDecision(scope, seed.Address.CustomerId, seed.Actor.StorefrontId,
            Guid.NewGuid(), false, DateTimeOffset.UtcNow.AddMinutes(2), consent.Id, contact.Authority.ResourceId,
            new(seed.Address.CustomerId, null));
        var owner = Substitute.For<IDeliveryContactScopeAuthorizer>();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var lockObserved = false;
        owner.RevalidateAsync(seed.AccountId, seed.Actor.StorefrontId, scope, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var token = call.ArgAt<CancellationToken>(3);
            var connectionString = await fixture.ReadAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token);
            await using var command = new NpgsqlCommand("SELECT 1 FROM customer_accounts WHERE \"Id\" = @id FOR UPDATE NOWAIT", connection);
            command.Parameters.AddWithValue("id", seed.AccountId);
            var refusal = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteScalarAsync(token));
            refusal.SqlState.Should().Be(PostgresErrorCodes.LockNotAvailable);
            lockObserved = true;
            return decision;
        });
        var result = await ActAsync(seed.Actor, store => store.ResolveRecipientAsync(seed.Actor, contact.Authority, consent.Id,
            scope, Guid.NewGuid(), null, Digest, budget.Token), owner);
        lockObserved.Should().BeTrue();
        result.RecipientName.Should().Be("Recipient");
        await owner.Received(1).RevalidateAsync(seed.AccountId, seed.Actor.StorefrontId, scope, Arg.Any<CancellationToken>());
    }

    private static DeliveryContactScope Scope() => new(Guid.NewGuid(), "opaque allocation", Guid.NewGuid(), 3, Guid.NewGuid(),
        new DeliveryContactAuthority("delivery.duty.v2", Guid.NewGuid(), 2, Digest));
}
