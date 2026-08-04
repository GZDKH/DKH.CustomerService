using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public sealed class StorefrontMembershipConfiguration : IEntityTypeConfiguration<StorefrontMembershipEntity>
{
    public void Configure(EntityTypeBuilder<StorefrontMembershipEntity> builder)
    {
        builder.ToTable("storefront_memberships");
        builder.HasKey(membership => membership.Id);

        builder.Ignore(membership => membership.DomainEvents);

        builder.Property(membership => membership.CustomerAccountId)
            .HasColumnName("customer_account_id")
            .IsRequired();
        builder.Property(membership => membership.StorefrontId)
            .HasColumnName("storefront_id")
            .IsRequired();
        builder.Property(membership => membership.LegacyCustomerProfileId)
            .HasColumnName("legacy_customer_profile_id");
        builder.Property(membership => membership.FirstAuthenticatedAt)
            .HasColumnName("first_authenticated_at")
            .IsRequired();
        builder.Property(membership => membership.LastAuthenticatedAt)
            .HasColumnName("last_authenticated_at")
            .IsRequired();
        builder.Property(membership => membership.LastActivityAt)
            .HasColumnName("last_activity_at")
            .IsRequired();
        builder.Property(membership => membership.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(StorefrontMembershipStatusType.Active)
            .IsRequired();

        builder.HasOne<CustomerAccountEntity>()
            .WithMany()
            .HasForeignKey(membership => membership.CustomerAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CustomerProfileEntity>()
            .WithOne()
            .HasForeignKey<StorefrontMembershipEntity>(membership => membership.LegacyCustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new { membership.CustomerAccountId, membership.StorefrontId })
            .IsUnique()
            .HasDatabaseName("ux_storefront_memberships_account_storefront");
        builder.HasIndex(membership => membership.LegacyCustomerProfileId)
            .IsUnique()
            .HasFilter("\"legacy_customer_profile_id\" IS NOT NULL")
            .HasDatabaseName("ux_storefront_memberships_legacy_profile");
        builder.HasIndex(membership => membership.StorefrontId)
            .HasDatabaseName("ix_storefront_memberships_storefront");

        builder.HasQueryFilter(membership => !membership.IsDeleted);
    }
}
