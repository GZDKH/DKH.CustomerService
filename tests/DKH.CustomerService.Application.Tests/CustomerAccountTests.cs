using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Domain.Events;
using DKH.Platform.MultiTenancy;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public class CustomerAccountTests
{
    private static readonly DateTime VerifiedAt = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithVerifiedIdentity_CreatesGlobalAccountWithoutStorefrontScope()
    {
        var account = CustomerAccountEntity.Create(
            "https://auth.xnata.com/realms/dkh/",
            " keycloak-subject ",
            " Customer@Example.COM ",
            " Ada ",
            " Lovelace ",
            "EN-US",
            VerifiedAt);

        account.Id.Should().NotBeEmpty();
        account.IdentityIssuer.Should().Be("https://auth.xnata.com/realms/dkh");
        account.IdentitySubject.Should().Be("keycloak-subject");
        account.VerifiedEmail.Should().Be("customer@example.com");
        account.EmailVerifiedAt.Should().Be(VerifiedAt);
        account.FirstName.Should().Be("Ada");
        account.LastName.Should().Be("Lovelace");
        account.PreferredLocale.Should().Be("en-us");
        account.Status.Should().Be(CustomerAccountStatusType.Active);
        account.Should().NotBeAssignableTo<IPlatformStorefrontScoped>();
        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CustomerAccountCreatedDomainEvent>();
    }

    [Theory]
    [InlineData("", "subject", "customer@example.com")]
    [InlineData("not-an-issuer", "subject", "customer@example.com")]
    [InlineData("https://auth.xnata.com/realms/dkh", "", "customer@example.com")]
    [InlineData("https://auth.xnata.com/realms/dkh", "subject", "not-an-email")]
    public void Create_WithInvalidAuthoritativeIdentity_Throws(
        string issuer,
        string subject,
        string email)
    {
        var act = () => CustomerAccountEntity.Create(
            issuer,
            subject,
            email,
            null,
            null,
            "en",
            VerifiedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkIdentity_WithFreshProviderProof_AddsNormalizedIdentity()
    {
        var account = CreateAccount();

        var identity = account.LinkIdentity(
            "TELEGRAM",
            " 123456789 ",
            "Telegram",
            " @ada ",
            VerifiedAt);

        identity.CustomerAccountId.Should().Be(account.Id);
        identity.ProviderAuthority.Should().Be("telegram");
        identity.ProviderSubject.Should().Be("123456789");
        identity.ProviderKind.Should().Be("telegram");
        identity.DisplayName.Should().Be("@ada");
        identity.VerifiedAt.Should().Be(VerifiedAt);
        account.LinkedIdentities.Should().ContainSingle();
    }

    [Fact]
    public void LinkIdentity_WhenAuthorityAndSubjectAlreadyLinked_Throws()
    {
        var account = CreateAccount();
        account.LinkIdentity("telegram", "123", "telegram", null, VerifiedAt);

        var act = () => account.LinkIdentity("TELEGRAM", "123", "telegram", null, VerifiedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LinkIdentity_WithOidcAuthority_PreservesSubjectNamespace()
    {
        var account = CreateAccount();

        var identity = account.LinkIdentity(
            "https://accounts.example.com/tenant/",
            "Pairwise-Subject",
            "oidc",
            null,
            VerifiedAt);

        identity.ProviderAuthority.Should().Be("https://accounts.example.com/tenant");
        identity.ProviderSubject.Should().Be("Pairwise-Subject");
    }

    [Fact]
    public void Create_WithValuesBeyondPersistenceBounds_ThrowsBeforeDatabaseWrite()
    {
        var overlongSubject = new string('s', 257);
        var overlongName = new string('n', 101);
        var overlongLocale = new string('l', 17);

        var subjectAction = () => CustomerAccountEntity.Create(
            "https://auth.xnata.com/realms/dkh",
            overlongSubject,
            "customer@example.com",
            null,
            null,
            "en",
            VerifiedAt);
        var nameAction = () => CustomerAccountEntity.Create(
            "https://auth.xnata.com/realms/dkh",
            "subject",
            "customer@example.com",
            overlongName,
            null,
            "en",
            VerifiedAt);
        var localeAction = () => CustomerAccountEntity.Create(
            "https://auth.xnata.com/realms/dkh",
            "subject",
            "customer@example.com",
            null,
            null,
            overlongLocale,
            VerifiedAt);

        subjectAction.Should().Throw<ArgumentException>();
        nameAction.Should().Throw<ArgumentException>();
        localeAction.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkIdentity_WithValuesBeyondPersistenceBounds_ThrowsBeforeDatabaseWrite()
    {
        var account = CreateAccount();

        var kindAction = () => account.LinkIdentity(
            "telegram",
            "123",
            new string('p', 33),
            null,
            VerifiedAt);
        var displayNameAction = () => account.LinkIdentity(
            "telegram",
            "123",
            "telegram",
            new string('d', 201),
            VerifiedAt);

        kindAction.Should().Throw<ArgumentException>();
        displayNameAction.Should().Throw<ArgumentException>();
        account.LinkedIdentities.Should().BeEmpty();
    }

    [Fact]
    public void CreateMembership_WithAuthenticatedTouch_CreatesSeparateStorefrontScopedAggregate()
    {
        var accountId = Guid.NewGuid();
        var storefrontId = Guid.NewGuid();
        var legacyProfileId = Guid.NewGuid();

        var membership = StorefrontMembershipEntity.Create(
            accountId,
            storefrontId,
            VerifiedAt,
            legacyProfileId);

        membership.CustomerAccountId.Should().Be(accountId);
        membership.StorefrontId.Should().Be(storefrontId);
        membership.LegacyCustomerProfileId.Should().Be(legacyProfileId);
        membership.FirstAuthenticatedAt.Should().Be(VerifiedAt);
        membership.LastAuthenticatedAt.Should().Be(VerifiedAt);
        membership.LastActivityAt.Should().Be(VerifiedAt);
        membership.Status.Should().Be(StorefrontMembershipStatusType.Active);
        membership.Should().BeAssignableTo<IPlatformStorefrontScoped>();
        membership.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StorefrontMembershipCreatedDomainEvent>();
    }

    [Fact]
    public void RegisterAuthenticatedTouch_WithOlderTimestamp_DoesNotMoveActivityBackwards()
    {
        var membership = StorefrontMembershipEntity.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            VerifiedAt);

        membership.RegisterAuthenticatedTouch(VerifiedAt.AddMinutes(5));
        membership.RegisterAuthenticatedTouch(VerifiedAt.AddMinutes(1));

        membership.LastAuthenticatedAt.Should().Be(VerifiedAt.AddMinutes(5));
        membership.LastActivityAt.Should().Be(VerifiedAt.AddMinutes(5));
    }

    [Fact]
    public void Unblock_WhenAccountIsPendingDeletion_DoesNotRestoreAccess()
    {
        var account = CreateAccount();
        account.MarkDeletionPending();

        var act = account.Unblock;

        act.Should().Throw<InvalidOperationException>();
        account.Status.Should().Be(CustomerAccountStatusType.DeletionPending);
    }

    [Fact]
    public void Activate_WhenMembershipIsRevoked_DoesNotRestoreAccess()
    {
        var membership = StorefrontMembershipEntity.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            VerifiedAt);
        membership.Revoke();

        var act = membership.Activate;

        act.Should().Throw<InvalidOperationException>();
        membership.Status.Should().Be(StorefrontMembershipStatusType.Revoked);
    }

    [Fact]
    public void LegacyProfileReconciliation_RequiresProofAndSupportsQuarantineRetry()
    {
        var profile = CustomerProfileEntity.Create(
            Guid.NewGuid(),
            "legacy-subject",
            "Ada");

        profile.AccountReconciliationStatus.Should().Be(CustomerAccountReconciliationStatusType.PendingProof);

        profile.BeginAccountReconciliation(VerifiedAt);
        profile.QuarantineAccountReconciliation("ambiguous_subject", VerifiedAt.AddSeconds(1));

        profile.CustomerAccountId.Should().BeNull();
        profile.AccountReconciliationAttemptCount.Should().Be(1);
        profile.AccountReconciliationStatus.Should().Be(CustomerAccountReconciliationStatusType.Quarantined);
        profile.AccountReconciliationReasonCode.Should().Be("ambiguous_subject");

        profile.RetryAccountReconciliation();

        profile.AccountReconciliationStatus.Should().Be(CustomerAccountReconciliationStatusType.PendingProof);
        profile.AccountReconciliationReasonCode.Should().BeNull();
    }

    [Fact]
    public void LegacyProfileReconciliation_WithProvenAccount_CompletesLinkedState()
    {
        var accountId = Guid.NewGuid();
        var profile = CustomerProfileEntity.Create(
            Guid.NewGuid(),
            "legacy-subject",
            "Ada");

        profile.BeginAccountReconciliation(VerifiedAt);
        profile.CompleteAccountReconciliation(accountId, VerifiedAt.AddSeconds(1));

        profile.CustomerAccountId.Should().Be(accountId);
        profile.AccountReconciliationStatus.Should().Be(CustomerAccountReconciliationStatusType.Linked);
        profile.AccountReconciliationAttemptCount.Should().Be(1);
        profile.AccountReconciliationReasonCode.Should().BeNull();
    }

    private static CustomerAccountEntity CreateAccount()
        => CustomerAccountEntity.Create(
            "https://auth.xnata.com/realms/dkh",
            "keycloak-subject",
            "customer@example.com",
            "Ada",
            "Lovelace",
            "en",
            VerifiedAt);
}
