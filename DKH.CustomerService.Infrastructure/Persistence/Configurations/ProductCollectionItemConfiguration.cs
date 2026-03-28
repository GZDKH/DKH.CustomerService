using DKH.CustomerService.Domain.Entities.ProductCollection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public class ProductCollectionItemConfiguration : IEntityTypeConfiguration<ProductCollectionItemEntity>
{
    public void Configure(EntityTypeBuilder<ProductCollectionItemEntity> builder)
    {
        builder.ToTable("product_collection_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.ProductSkuId);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.Rating);
        builder.Property(x => x.AddedAt).IsRequired();

        builder.HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.Status });
    }
}
