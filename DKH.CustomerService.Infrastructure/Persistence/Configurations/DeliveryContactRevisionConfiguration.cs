using DKH.CustomerService.Domain.Entities.DeliveryContact;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public sealed class DeliveryContactRevisionConfiguration : IEntityTypeConfiguration<DeliveryContactRevisionEntity>
{
    public void Configure(EntityTypeBuilder<DeliveryContactRevisionEntity> builder)
    {
        builder.ToTable("delivery_contact_revisions", table => table.HasCheckConstraint("ck_delivery_contact_revision", "\"Revision\" > 0 AND \"Digest\" ~ '^[0-9a-f]{64}$' AND \"SourceDigest\" ~ '^[0-9a-f]{64}$'"));
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Digest).HasMaxLength(64).IsRequired();
        builder.Property(row => row.SourceDigest).HasMaxLength(64).IsRequired();
        builder.Property(row => row.RequestDigest).HasMaxLength(64).IsRequired();
        builder.HasIndex(row => new { row.OwnerAccountId, row.OperationId }).IsUnique();
        builder.HasIndex(row => new { row.OwnerAccountId, row.StorefrontId, row.AddressId, row.Revision }).IsUnique();
        // Intentionally no cascading FK to mutable/deletable source accounts,
        // profiles or addresses. Retained metadata cannot resurrect their data.
    }
}
