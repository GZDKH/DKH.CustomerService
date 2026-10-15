using DKH.CustomerService.Domain.Entities.DeliveryContact;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public sealed class DeliveryContactLeaseConfiguration : IEntityTypeConfiguration<DeliveryContactLeaseEntity>
{
    public void Configure(EntityTypeBuilder<DeliveryContactLeaseEntity> builder)
    {
        builder.ToTable("delivery_contact_leases", table => table.HasCheckConstraint("ck_delivery_contact_lease", "\"ExpiresAtUtc\" > \"IssuedAtUtc\" AND \"ExpiresAtUtc\" <= \"IssuedAtUtc\" + interval '5 minutes' AND ((\"Purpose\" = 1 AND \"ConsentId\" IS NULL AND \"ConsentRevision\" IS NULL AND \"WorkspaceId\" IS NULL) OR (\"Purpose\" = 5 AND \"ConsentId\" IS NOT NULL AND \"ConsentRevision\" > 0 AND \"WorkspaceId\" IS NOT NULL))"));
        builder.HasKey(row => row.Id);
        builder.Property(row => row.ContextDigest).HasMaxLength(64).IsRequired();
        builder.Property(row => row.RequestDigest).HasMaxLength(64).IsRequired();
        builder.HasIndex(row => new { row.ActorAccountId, row.OperationId }).IsUnique();
        builder.HasOne<DeliveryContactRevisionEntity>().WithMany().HasForeignKey(row => row.ContactRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeliveryContactConsentEntity>().WithMany().HasForeignKey(row => row.ConsentId).OnDelete(DeleteBehavior.Restrict);
    }
}
