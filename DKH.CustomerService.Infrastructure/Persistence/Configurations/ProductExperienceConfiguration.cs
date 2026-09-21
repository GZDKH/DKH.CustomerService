using DKH.CustomerService.Domain.Entities.ProductCollection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public sealed class ProductExperienceConfiguration : IEntityTypeConfiguration<ProductExperienceEntity>
{
    public void Configure(EntityTypeBuilder<ProductExperienceEntity> builder)
    {
        builder.ToTable("product_collection_experiences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CollectionItemId).IsRequired();
        builder.Property(x => x.ExperiencedAt);
        builder.Property(x => x.PersonalText).HasMaxLength(7000);
        builder.Property(x => x.Recommendation).HasMaxLength(2000);
        builder.HasIndex(x => x.CollectionItemId).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Observations)
            .WithOne()
            .HasForeignKey(x => x.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Tags)
            .WithOne()
            .HasForeignKey(x => x.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProductExperienceObservationConfiguration : IEntityTypeConfiguration<ProductExperienceObservationEntity>
{
    public void Configure(EntityTypeBuilder<ProductExperienceObservationEntity> builder)
    {
        builder.ToTable("product_experience_observations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExperienceId).IsRequired();
        builder.Property(x => x.DefinitionId).IsRequired();
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ValueType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.TextValue).HasMaxLength(2000);
        builder.Property(x => x.DecimalValue).HasPrecision(18, 6);
        builder.Property(x => x.UnitCode).HasMaxLength(32);
        builder.HasIndex(x => new { x.ExperienceId, x.DefinitionId, x.Role }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class ProductExperienceTagConfiguration : IEntityTypeConfiguration<ProductExperienceTagEntity>
{
    public void Configure(EntityTypeBuilder<ProductExperienceTagEntity> builder)
    {
        builder.ToTable("product_experience_tags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExperienceId).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ExperienceId, x.Value }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
