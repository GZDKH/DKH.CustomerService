using DKH.CustomerService.Domain.Entities.CustomerAccount;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public sealed class LinkedCustomerIdentityConfiguration : IEntityTypeConfiguration<LinkedCustomerIdentityEntity>
{
    public void Configure(EntityTypeBuilder<LinkedCustomerIdentityEntity> builder)
    {
        builder.ToTable("linked_customer_identities");
        builder.HasKey(identity => identity.Id);

        builder.Property(identity => identity.CustomerAccountId)
            .HasColumnName("customer_account_id")
            .IsRequired();
        builder.Property(identity => identity.ProviderAuthority)
            .HasColumnName("provider_authority")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(identity => identity.ProviderSubject)
            .HasColumnName("provider_subject")
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(identity => identity.ProviderKind)
            .HasColumnName("provider_kind")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(identity => identity.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200);
        builder.Property(identity => identity.LinkedAt)
            .HasColumnName("linked_at")
            .IsRequired();
        builder.Property(identity => identity.VerifiedAt)
            .HasColumnName("verified_at")
            .IsRequired();
        builder.Property(identity => identity.LegacyExternalIdentityId)
            .HasColumnName("legacy_external_identity_id");

        builder.HasIndex(identity => new { identity.ProviderAuthority, identity.ProviderSubject })
            .IsUnique()
            .HasDatabaseName("ux_linked_customer_identities_authority_subject");
        builder.HasIndex(identity => identity.CustomerAccountId)
            .HasDatabaseName("ix_linked_customer_identities_account");
        builder.HasIndex(identity => identity.LegacyExternalIdentityId)
            .IsUnique()
            .HasFilter("\"legacy_external_identity_id\" IS NOT NULL")
            .HasDatabaseName("ux_linked_customer_identities_legacy");

        builder.HasQueryFilter(identity => !identity.IsDeleted);
    }
}
