using DKH.CustomerService.Domain.Entities.CustomerAddress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddressEntity>
{
    public void Configure(EntityTypeBuilder<CustomerAddressEntity> builder)
    {
        builder.ToTable("customer_addresses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Country).HasMaxLength(100).IsRequired();
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Street).HasMaxLength(256);
        builder.Property(x => x.Building).HasMaxLength(32);
        builder.Property(x => x.Apartment).HasMaxLength(32);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.IsDefault).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.Province).HasMaxLength(100);
        builder.Property(x => x.Company).HasMaxLength(256);

        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.CustomerId, x.IsDefault }).HasFilter("\"IsDefault\" = true");
    }
}
