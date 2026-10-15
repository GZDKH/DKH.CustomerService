using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class DeliveryContactAuthorityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private const string RequestDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ExplicitObservationBindsActualOwnerProfileAddressAndContentWithoutStoringPii()
    {
        var address = Address();
        var owner = Guid.NewGuid();
        var storefront = Guid.NewGuid();
        var revision = Observe(address, owner, storefront);
        revision.Revision.Should().Be(1);
        revision.LegacyProfileId.Should().Be(address.CustomerId);
        revision.AddressId.Should().Be(address.Id);
        revision.OwnerAccountId.Should().Be(owner);
        revision.MatchesCurrent(owner, storefront, address).Should().BeTrue();
        revision.MatchesCurrent(Guid.NewGuid(), storefront, address).Should().BeFalse();
        revision.MatchesCurrent(owner, Guid.NewGuid(), address).Should().BeFalse();
        revision.Digest.Should().MatchRegex("^[a-f0-9]{64}$");
        revision.SourceDigest.Should().NotBe(revision.Digest);
        typeof(DeliveryContactRevisionEntity).GetProperties().Select(property => property.Name)
            .Should().NotContain(["Phone", "Email", "Street", "FirstName", "LastName"]);
    }

    [Theory]
    [InlineData("city")]
    [InlineData("phone")]
    [InlineData("name")]
    [InlineData("purge")]
    public void ChangedOrPurgedCurrentAddressCannotSatisfyRetainedAuthority(string change)
    {
        var address = Address();
        var revision = Observe(address);
        var digest = revision.Digest;
        switch (change)
        {
            case "city":
                address.Update(city: "Different city");
                break;
            case "phone":
                address.Update(phone: "+999");
                break;
            case "name":
                address.Update(firstName: "Different recipient");
                break;
            case "purge":
                address.Anonymize();
                break;
        }

        revision.MatchesCurrent(revision.OwnerAccountId, revision.StorefrontId, address).Should().BeFalse();
        revision.Digest.Should().Be(digest);
        revision.Revision.Should().Be(1);
    }

    [Fact]
    public void LabelAndDefaultPresentationDoNotChangeContactTuple()
    {
        var address = Address();
        var revision = Observe(address);
        address.Update(label: "Renamed presentation");
        address.SetDefault(true);
        revision.MatchesCurrent(revision.OwnerAccountId, revision.StorefrontId, address).Should().BeTrue();
    }

    [Fact]
    public void NewExplicitObservationHasItsOwnPersistedRevisionRatherThanTimestampVersion()
    {
        var address = Address();
        var first = Observe(address);
        var next = DeliveryContactRevisionEntity.Observe(first.OwnerAccountId, first.StorefrontId, address, 2, Guid.NewGuid(), Now, RequestDigest);
        next.ObservedAtUtc.Should().Be(first.ObservedAtUtc);
        next.Revision.Should().Be(2);
        next.Id.Should().NotBe(first.Id);
        next.SourceDigest.Should().Be(first.SourceDigest);
        next.Digest.Should().NotBe(first.Digest);
    }

    [Fact]
    public void RecipientConsentBindsEveryActualScopeVersionAndDutyAuthority()
    {
        var contact = Observe(Address());
        var scope = Scope();
        var consent = Consent(contact, scope);
        consent.Permits(contact, scope, Now).Should().BeTrue();
        consent.Permits(contact, scope with { PlanRevisionId = Guid.NewGuid() }, Now).Should().BeFalse();
        consent.Permits(contact, scope with { ChoiceRevisionId = Guid.NewGuid() }, Now).Should().BeFalse();
        consent.Permits(contact, scope with { ScopeRevision = 2 }, Now).Should().BeFalse();
        consent.Permits(contact, scope with { AllocationPublicRef = "another-allocation" }, Now).Should().BeFalse();
        consent.Permits(contact, scope with { Duty = scope.Duty with { Revision = 2 } }, Now).Should().BeFalse();
        consent.Permits(contact, scope, Now.AddMinutes(3)).Should().BeFalse();
    }

    [Fact]
    public void RevocationIsVersionedIdempotentAndCannotBeReplayedWithChangedIntent()
    {
        var contact = Observe(Address());
        var scope = Scope();
        var consent = Consent(contact, scope);
        var operation = Guid.NewGuid();
        consent.Revoke(operation, 1, Now.AddSeconds(1), RequestDigest);
        consent.Revoke(operation, 1, Now.AddSeconds(2), RequestDigest);
        consent.Revision.Should().Be(2);
        consent.RevokedAtUtc.Should().Be(Now.AddSeconds(1));
        consent.Permits(contact, scope, Now.AddSeconds(2)).Should().BeFalse();
        var changed = () => consent.Revoke(operation, 1, Now.AddSeconds(2), new string('b', 64));
        changed.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OwnerScopedRetainedRevocationCannotAuthorizeDifferentTargetBodyOrExpectedRevision()
    {
        var contact = Observe(Address());
        var first = Consent(contact, Scope());
        var second = Consent(contact, Scope());
        var operation = Guid.NewGuid();
        first.Revoke(operation, 1, Now.AddSeconds(1), RequestDigest);
        first.MatchesRevocationReplay(first.Id, operation, 1, RequestDigest).Should().BeTrue();
        first.MatchesRevocationReplay(second.Id, operation, 1, RequestDigest).Should().BeFalse();
        first.MatchesRevocationReplay(first.Id, operation, 1, new string('b', 64)).Should().BeFalse();
        first.MatchesRevocationReplay(first.Id, operation, 2, RequestDigest).Should().BeFalse();
        first.MatchesRevocationReplay(first.Id, Guid.NewGuid(), 1, RequestDigest).Should().BeFalse();
        second.RevokedAtUtc.Should().BeNull();
        second.Revision.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void SessionUsesEarlierRealAuthorityExpiryAndNeverExceedsFiveMinutes(int authorityMinutes)
    {
        var contact = Observe(Address());
        var lease = DeliveryContactLeaseEntity.Issue(contact.OwnerAccountId, contact, DeliveryContactPurpose.OwnLiabilityCoordination,
            RequestDigest, RequestDigest, Guid.NewGuid(), Now, Now.AddMinutes(authorityMinutes));
        lease.ExpiresAtUtc.Should().Be(Now.AddMinutes(Math.Min(authorityMinutes, 5)));
        lease.MatchesAccess(contact.OwnerAccountId, contact.StorefrontId, DeliveryContactPurpose.OwnLiabilityCoordination,
            RequestDigest, lease.ExpiresAtUtc).Should().BeFalse();
        lease.MatchesAccess(Guid.NewGuid(), contact.StorefrontId, DeliveryContactPurpose.OwnLiabilityCoordination,
            RequestDigest, Now).Should().BeFalse();
        lease.MatchesAccess(contact.OwnerAccountId, contact.StorefrontId, DeliveryContactPurpose.LocalShipment,
            RequestDigest, Now).Should().BeFalse();
    }

    [Fact]
    public void RecipientLeaseRequiresConsentRealWorkspaceAndExactCurrentContactTuple()
    {
        var contact = Observe(Address());
        var scope = Scope();
        var consent = Consent(contact, scope);
        var actor = Guid.NewGuid();
        var workspace = Guid.NewGuid();
        var lease = DeliveryContactLeaseEntity.Issue(actor, contact, DeliveryContactPurpose.LocalShipment, scope.Digest(),
            RequestDigest, Guid.NewGuid(), Now, Now.AddMinutes(4), consent, workspace);
        lease.ExpiresAtUtc.Should().Be(consent.ValidUntilUtc);
        lease.WorkspaceId.Should().Be(workspace);
        var absent = () => DeliveryContactLeaseEntity.Issue(actor, contact, DeliveryContactPurpose.LocalShipment, scope.Digest(),
            RequestDigest, Guid.NewGuid(), Now, Now.AddMinutes(4), consent);
        absent.Should().Throw<ArgumentException>();
        consent.Revoke(Guid.NewGuid(), 1, Now, RequestDigest);
        var revoked = () => DeliveryContactLeaseEntity.Issue(actor, contact, DeliveryContactPurpose.LocalShipment, scope.Digest(),
            RequestDigest, Guid.NewGuid(), Now, Now.AddMinutes(4), consent, workspace);
        revoked.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ForeignContactCannotBecomeOwnLiabilityContact()
    {
        var contact = Observe(Address());
        var issue = () => DeliveryContactLeaseEntity.Issue(Guid.NewGuid(), contact, DeliveryContactPurpose.OwnLiabilityCoordination,
            RequestDigest, RequestDigest, Guid.NewGuid(), Now, Now.AddMinutes(5));
        issue.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ContactAuthorityRejectsFabricatedRevisionAndMalformedDigest()
    {
        var invalid = () => new DeliveryContactAuthority("customer.delivery-contact.v1", Guid.NewGuid(), 0, RequestDigest).Validate();
        invalid.Should().Throw<ArgumentException>();
        invalid = () => new DeliveryContactAuthority("customer.delivery-contact.v1", Guid.NewGuid(), 1, new string('A', 64)).Validate();
        invalid.Should().Throw<ArgumentException>();
    }

    private static CustomerAddressEntity Address() => CustomerAddressEntity.Create(Guid.NewGuid(), "Home", "GB", "London",
        street: "A street", phone: "+123", firstName: "Ada", lastName: "Lovelace");
    private static DeliveryContactRevisionEntity Observe(CustomerAddressEntity address, Guid? owner = null, Guid? storefront = null)
        => DeliveryContactRevisionEntity.Observe(owner ?? Guid.NewGuid(), storefront ?? Guid.NewGuid(), address, 1, Guid.NewGuid(), Now, RequestDigest);
    private static DeliveryContactScope Scope() => new(Guid.NewGuid(), "opaque-allocation", Guid.NewGuid(), 1, Guid.NewGuid(),
        new DeliveryContactAuthority("delivery.duty.v2", Guid.NewGuid(), 1, RequestDigest));
    private static DeliveryContactConsentEntity Consent(DeliveryContactRevisionEntity contact, DeliveryContactScope scope)
        => DeliveryContactConsentEntity.Record(contact, scope, Guid.NewGuid(), Now, Now.AddMinutes(3), RequestDigest);
}
