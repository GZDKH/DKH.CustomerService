using DKH.CustomerService.Domain.Entities.WishlistItem;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItemEntity>
{
    public void Configure(EntityTypeBuilder<WishlistItemEntity> builder)
    {
        builder.ToTable("wishlist_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.ProductSkuId);
        builder.Property(x => x.AddedAt).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(512);

        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.CustomerId, x.ProductId, x.ProductSkuId }).IsUnique();
    }
}
