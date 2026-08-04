using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccount.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.IntegrationTests.Integration.CustomerAccounts;

public sealed class GlobalCustomerAccountHandlerTests
{
    private const string Issuer = "https://auth.xnata.com/realms/dkh";
    private static readonly DateTime VerifiedAt = new(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EnsureAccountAndMembership_AreIdempotent_AndReconcileLegacyProfileAsync()
    {
        await using var context = CreateContext();
        var storefrontId = Guid.NewGuid();
        var profile = CustomerProfileEntity.Create(storefrontId, "account-subject", "Legacy");
        profile.AddExternalIdentity("Telegram", "123456", displayName: "@customer", isPrimary: true);
        context.CustomerProfiles.Add(profile);
        await context.SaveChangesAsync();

        var accountHandler = new EnsureCustomerAccountCommandHandler(context);
        var firstAccount = await accountHandler.Handle(EnsureAccount("account-subject"), CancellationToken.None);
        var secondAccount = await accountHandler.Handle(EnsureAccount("account-subject"), CancellationToken.None);
        var membershipHandler = new EnsureStorefrontMembershipCommandHandler(context);
        var firstMembership = await membershipHandler.Handle(
            new EnsureStorefrontMembershipCommand(Identity("account-subject"), storefrontId, VerifiedAt),
            CancellationToken.None);
        var secondMembership = await membershipHandler.Handle(
            new EnsureStorefrontMembershipCommand(Identity("account-subject"), storefrontId, VerifiedAt.AddMinutes(1)),
            CancellationToken.None);

        firstAccount.Id.Value.Should().Be(secondAccount.Id.Value);
        firstMembership.Id.Value.Should().Be(secondMembership.Id.Value);
        (await context.CustomerAccounts.CountAsync()).Should().Be(1);
        (await context.StorefrontMemberships.CountAsync()).Should().Be(1);

        var reconciledProfile = await context.CustomerProfiles.SingleAsync();
        reconciledProfile.CustomerAccountId.Should().Be(Guid.Parse(firstAccount.Id.Value));
        reconciledProfile.AccountReconciliationStatus.Should().Be(CustomerAccountReconciliationStatusType.Linked);
        (await context.LinkedCustomerIdentities.SingleAsync()).ProviderSubject.Should().Be("123456");
    }

    [Fact]
    public async Task ConsolidatedWishlist_ContainsOnlyPrincipalAccountItems_WithSourceStorefrontsAsync()
    {
        await using var context = CreateContext();
        var firstStorefrontId = Guid.NewGuid();
        var secondStorefrontId = Guid.NewGuid();
        var unrelatedStorefrontId = Guid.NewGuid();
        var firstProfile = CustomerProfileEntity.Create(firstStorefrontId, "principal", "First");
        var secondProfile = CustomerProfileEntity.Create(secondStorefrontId, "principal", "Second");
        var unrelatedProfile = CustomerProfileEntity.Create(unrelatedStorefrontId, "other", "Other");
        context.CustomerProfiles.AddRange(firstProfile, secondProfile, unrelatedProfile);
        context.WishlistItems.AddRange(
            WishlistItemEntity.Create(firstProfile.Id, Guid.NewGuid()),
            WishlistItemEntity.Create(secondProfile.Id, Guid.NewGuid()),
            WishlistItemEntity.Create(unrelatedProfile.Id, Guid.NewGuid()));
        await context.SaveChangesAsync();

        await EnsureAccountAsync(context, "principal");
        await EnsureAccountAsync(context, "other");
        var membershipHandler = new EnsureStorefrontMembershipCommandHandler(context);
        await membershipHandler.Handle(
            new EnsureStorefrontMembershipCommand(Identity("principal"), firstStorefrontId, VerifiedAt),
            CancellationToken.None);
        await membershipHandler.Handle(
            new EnsureStorefrontMembershipCommand(Identity("principal"), secondStorefrontId, VerifiedAt),
            CancellationToken.None);
        await membershipHandler.Handle(
            new EnsureStorefrontMembershipCommand(Identity("other"), unrelatedStorefrontId, VerifiedAt),
            CancellationToken.None);

        var response = await new ListConsolidatedWishlistEntriesQueryHandler(context).Handle(
            new ListConsolidatedWishlistEntriesQuery(Identity("principal"), 1, 20),
            CancellationToken.None);

        response.TotalCount.Should().Be(2);
        response.Items.Select(item => Guid.Parse(item.StorefrontId.Value)).Should().BeEquivalentTo(
            [firstStorefrontId, secondStorefrontId]);
    }

    [Fact]
    public async Task MembershipDeletion_RemovesOnlySelectedStorefrontDataAsync()
    {
        await using var context = CreateContext();
        var deletedStorefrontId = Guid.NewGuid();
        var retainedStorefrontId = Guid.NewGuid();
        var deletedProfile = CustomerProfileEntity.Create(deletedStorefrontId, "principal", "Delete me");
        deletedProfile.AddExternalIdentity("telegram", "111", email: "pii@example.com", isPrimary: true);
        var retainedProfile = CustomerProfileEntity.Create(retainedStorefrontId, "principal", "Keep me");
        context.CustomerProfiles.AddRange(deletedProfile, retainedProfile);
        context.WishlistItems.Add(WishlistItemEntity.Create(deletedProfile.Id, Guid.NewGuid(), note: "private"));
        await context.SaveChangesAsync();
        await EnsureAccountAsync(context, "principal");

        var membershipHandler = new EnsureStorefrontMembershipCommandHandler(context);
        await membershipHandler.Handle(
            new EnsureStorefrontMembershipCommand(Identity("principal"), deletedStorefrontId, VerifiedAt),
            CancellationToken.None);
        await membershipHandler.Handle(
            new EnsureStorefrontMembershipCommand(Identity("principal"), retainedStorefrontId, VerifiedAt),
            CancellationToken.None);

        await new DeleteStorefrontMembershipCommandHandler(context).Handle(
            new DeleteStorefrontMembershipCommand(Identity("principal"), deletedStorefrontId),
            CancellationToken.None);

        (await context.CustomerAccounts.CountAsync()).Should().Be(1);
        (await context.StorefrontMemberships.CountAsync()).Should().Be(1);
        (await context.StorefrontMemberships.SingleAsync()).StorefrontId.Should().Be(retainedStorefrontId);
        (await context.CustomerProfiles.CountAsync()).Should().Be(1);
        (await context.CustomerProfiles.SingleAsync()).Id.Should().Be(retainedProfile.Id);

        var deletedProfileState = await context.CustomerProfiles
            .IgnoreQueryFilters()
            .SingleAsync(profile => profile.Id == deletedProfile.Id);
        deletedProfileState.IsDeleted.Should().BeTrue();
        deletedProfileState.UserId.Should().StartWith("deleted:");
        (await context.ExternalIdentities.IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeTrue();
        (await context.WishlistItems.IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task FullAccountDeletion_AnonymizesAllMembershipData_AndAllowsFreshAccountLaterAsync()
    {
        await using var context = CreateContext();
        var storefrontId = Guid.NewGuid();
        var profile = CustomerProfileEntity.Create(storefrontId, "principal", "Private", email: "private@example.com");
        profile.AddExternalIdentity("telegram", "777", email: "private@example.com", isPrimary: true);
        context.CustomerProfiles.Add(profile);
        context.WishlistItems.Add(WishlistItemEntity.Create(profile.Id, Guid.NewGuid(), note: "private"));
        await context.SaveChangesAsync();
        await EnsureAccountAsync(context, "principal");
        await new EnsureStorefrontMembershipCommandHandler(context).Handle(
            new EnsureStorefrontMembershipCommand(Identity("principal"), storefrontId, VerifiedAt),
            CancellationToken.None);

        await new DeleteCustomerAccountDataCommandHandler(context).Handle(
            new DeleteCustomerAccountDataCommand(Identity("principal")),
            CancellationToken.None);

        (await context.CustomerAccounts.CountAsync()).Should().Be(0);
        (await context.StorefrontMemberships.CountAsync()).Should().Be(0);
        (await context.CustomerProfiles.CountAsync()).Should().Be(0);
        (await context.LinkedCustomerIdentities.CountAsync()).Should().Be(0);

        var deletedAccount = await context.CustomerAccounts.IgnoreQueryFilters().SingleAsync();
        deletedAccount.IsDeleted.Should().BeTrue();
        deletedAccount.IdentityIssuer.Should().Be("https://deleted.invalid");
        deletedAccount.IdentitySubject.Should().StartWith("deleted:");
        deletedAccount.VerifiedEmail.Should().EndWith("@invalid.local");

        var recreated = await EnsureAccountAsync(context, "principal");
        recreated.Id.Value.Should().NotBe(deletedAccount.Id.ToString());
        (await context.CustomerAccounts.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task LegacyIdentityConflict_IsQuarantined_InsteadOfCrossLinkingAccountsAsync()
    {
        await using var context = CreateContext();
        await EnsureAccountAsync(context, "first");
        await EnsureAccountAsync(context, "second");
        var firstAccount = await context.CustomerAccounts.SingleAsync(account => account.IdentitySubject == "first");
        firstAccount.LinkIdentity("telegram", "shared-id", "telegram", null, VerifiedAt);
        var storefrontId = Guid.NewGuid();
        var secondProfile = CustomerProfileEntity.Create(storefrontId, "second", "Second");
        secondProfile.AddExternalIdentity("telegram", "shared-id", isPrimary: true);
        context.CustomerProfiles.Add(secondProfile);
        await context.SaveChangesAsync();

        var action = () => new EnsureStorefrontMembershipCommandHandler(context).Handle(
            new EnsureStorefrontMembershipCommand(Identity("second"), storefrontId, VerifiedAt),
            CancellationToken.None);

        await action.Should().ThrowAsync<CustomerAccountConflictException>();
        secondProfile.AccountReconciliationStatus.Should().Be(CustomerAccountReconciliationStatusType.Quarantined);
        secondProfile.AccountReconciliationReasonCode.Should().Be("external_identity_conflict");
        (await context.StorefrontMemberships.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BlockedAccount_CannotUseSelfServiceQueriesAsync()
    {
        await using var context = CreateContext();
        await EnsureAccountAsync(context, "principal");
        (await context.CustomerAccounts.SingleAsync()).Block();
        await context.SaveChangesAsync();

        var action = () => new GetCustomerAccountQueryHandler(context).Handle(
            new GetCustomerAccountQuery(Identity("principal")),
            CancellationToken.None);

        await action.Should().ThrowAsync<CustomerAccountAccessException>();
    }

    [Fact]
    public void PublicRequests_DoNotAcceptOwnershipOrStorefrontIdentifiers()
    {
        var forbiddenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "account_id",
            "customer_account_id",
            "user_id",
            "identity_issuer",
            "identity_subject",
            "storefront_id",
            "owner_id",
        };
        var requestDescriptors = new[]
        {
            EnsureCustomerAccountRequest.Descriptor,
            GetCustomerAccountRequest.Descriptor,
            UpdateCustomerAccountRequest.Descriptor,
            EnsureStorefrontMembershipRequest.Descriptor,
            ListStorefrontMembershipsRequest.Descriptor,
            ListLinkedCustomerIdentitiesRequest.Descriptor,
            ListConsolidatedWishlistEntriesRequest.Descriptor,
        };

        requestDescriptors
            .SelectMany(descriptor => descriptor.Fields.InDeclarationOrder())
            .Select(field => field.Name)
            .Should()
            .NotContain(field => forbiddenFields.Contains(field));
    }

    [Fact]
    public void CustomerAccountEndpoint_RequiresAuthentication_WithoutAdminOnlyPolicy()
    {
        var authorize = typeof(CustomerAccountGrpcService)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().BeNull();
    }

    private static EnsureCustomerAccountCommand EnsureAccount(string subject)
        => new(new VerifiedCustomerAccountIdentity(
            Issuer,
            subject,
            $"{subject}@example.com",
            VerifiedAt,
            "Test",
            "Customer",
            "en"));

    private static CustomerAccountIdentity Identity(string subject) => new(Issuer, subject);

    private static Task<CustomerAccountModel> EnsureAccountAsync(AppDbContext context, string subject)
        => new EnsureCustomerAccountCommandHandler(context).Handle(EnsureAccount(subject), CancellationToken.None);

    private static TestAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    private sealed class TestAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override Guid? GetCurrentUserId() => null;
    }
}
