using DKH.CustomerService.Domain.Entities.DeliveryContact;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public sealed class DeliveryContactConsentConfiguration : IEntityTypeConfiguration<DeliveryContactConsentEntity>
{
    public void Configure(EntityTypeBuilder<DeliveryContactConsentEntity> builder)
    {
        builder.ToTable("delivery_contact_consents", table => table.HasCheckConstraint("ck_delivery_contact_consent", "\"Revision\" > 0 AND \"ValidUntilUtc\" > \"CreatedAtUtc\" AND \"ValidUntilUtc\" <= \"CreatedAtUtc\" + interval '5 minutes' AND ((\"RevokedAtUtc\" IS NULL AND \"RevokeOperationId\" IS NULL AND \"Revision\" = 1) OR (\"RevokedAtUtc\" IS NOT NULL AND \"RevokeOperationId\" IS NOT NULL AND \"Revision\" = 2))"));
        builder.HasKey(row => row.Id);
        builder.Property(row => row.ScopeJson).HasColumnType("jsonb").IsRequired();
        builder.Property(row => row.ScopeDigest).HasMaxLength(64).IsRequired();
        builder.Property(row => row.ContactDigest).HasMaxLength(64).IsRequired();
        builder.Property(row => row.RequestDigest).HasMaxLength(64).IsRequired();
        builder.Property(row => row.RevokeRequestDigest).HasMaxLength(64);
        builder.HasIndex(row => new { row.OwnerAccountId, row.OperationId }).IsUnique();
        builder.HasIndex(row => new { row.OwnerAccountId, row.RevokeOperationId }).IsUnique()
            .HasDatabaseName("ux_delivery_contact_owner_revoke_operation")
            .HasFilter("\"RevokeOperationId\" IS NOT NULL");
        builder.HasOne<DeliveryContactRevisionEntity>().WithMany().HasForeignKey(row => row.ContactRevisionId).OnDelete(DeleteBehavior.Restrict);
    }
}
