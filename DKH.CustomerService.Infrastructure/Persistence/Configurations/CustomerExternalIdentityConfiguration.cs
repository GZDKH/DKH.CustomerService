using DKH.CustomerService.Domain.Entities.ExternalIdentity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public class CustomerExternalIdentityConfiguration : IEntityTypeConfiguration<CustomerExternalIdentityEntity>
{
    public void Configure(EntityTypeBuilder<CustomerExternalIdentityEntity> builder)
    {
        builder.ToTable("customer_external_identities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderUserId).HasColumnName("provider_user_id").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(x => x.IsPrimary).HasColumnName("is_primary").HasDefaultValue(false);
        builder.Property(x => x.LinkedAt).HasColumnName("linked_at").IsRequired();

        builder.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.Provider, x.Email });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
