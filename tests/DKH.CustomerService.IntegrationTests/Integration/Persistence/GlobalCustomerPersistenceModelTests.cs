using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.IntegrationTests.Integration.Persistence;

public sealed class GlobalCustomerPersistenceModelTests
{
    [Fact]
    public void CustomerAccount_UsesAuthoritativeSubjectAsOnlyUniqueCoreIdentity()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(CustomerAccountEntity));

        entity.Should().NotBeNull();
        entity!.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(CustomerAccountEntity.IdentityIssuer),
                    nameof(CustomerAccountEntity.IdentitySubject),
                }));
        entity.GetIndexes().Should().ContainSingle(index =>
            !index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(CustomerAccountEntity.VerifiedEmail) }));
    }

    [Fact]
    public void LinkedIdentityAndMembership_HaveGlobalAndStorefrontUniquenessConstraints()
    {
        using var context = CreateContext();
        var identity = context.Model.FindEntityType(typeof(LinkedCustomerIdentityEntity));
        var membership = context.Model.FindEntityType(typeof(StorefrontMembershipEntity));

        identity.Should().NotBeNull();
        identity!.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(LinkedCustomerIdentityEntity.ProviderAuthority),
                    nameof(LinkedCustomerIdentityEntity.ProviderSubject),
                }));

        membership.Should().NotBeNull();
        membership!.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(StorefrontMembershipEntity.CustomerAccountId),
                    nameof(StorefrontMembershipEntity.StorefrontId),
                }));
        membership.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(StorefrontMembershipEntity.LegacyCustomerProfileId) }));
    }

    [Fact]
    public void LegacyProfile_KeepsNullableAccountLinkAndRestartableReconciliationIndex()
    {
        using var context = CreateContext();
        var profile = context.Model.FindEntityType(typeof(CustomerProfileEntity));

        profile.Should().NotBeNull();
        profile!.FindProperty(nameof(CustomerProfileEntity.CustomerAccountId))!.IsNullable.Should().BeTrue();
        profile.GetIndexes().Should().ContainSingle(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(CustomerProfileEntity.AccountReconciliationStatus),
                    nameof(CustomerProfileEntity.LastAccountReconciliationAttemptAt),
                }));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
