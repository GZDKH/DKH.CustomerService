using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public sealed class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccountEntity>
{
    public void Configure(EntityTypeBuilder<CustomerAccountEntity> builder)
    {
        builder.ToTable("customer_accounts");
        builder.HasKey(account => account.Id);

        builder.Ignore(account => account.DomainEvents);

        builder.Property(account => account.IdentityIssuer)
            .HasColumnName("identity_issuer")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(account => account.IdentitySubject)
            .HasColumnName("identity_subject")
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(account => account.VerifiedEmail)
            .HasColumnName("verified_email")
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(account => account.EmailVerifiedAt)
            .HasColumnName("email_verified_at")
            .IsRequired();
        builder.Property(account => account.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100);
        builder.Property(account => account.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100);
        builder.Property(account => account.PreferredLocale)
            .HasColumnName("preferred_locale")
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(account => account.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(CustomerAccountStatusType.Active)
            .IsRequired();

        builder.HasMany(account => account.LinkedIdentities)
            .WithOne()
            .HasForeignKey(identity => identity.CustomerAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(account => new { account.IdentityIssuer, account.IdentitySubject })
            .IsUnique()
            .HasDatabaseName("ux_customer_accounts_issuer_subject");
        builder.HasIndex(account => account.VerifiedEmail)
            .HasDatabaseName("ix_customer_accounts_verified_email");

        builder.HasQueryFilter(account => !account.IsDeleted);
    }
}
