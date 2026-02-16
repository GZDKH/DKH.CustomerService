using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

public class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfileEntity>
{
    public void Configure(EntityTypeBuilder<CustomerProfileEntity> builder)
    {
        builder.ToTable("customer_profiles");
        builder.HasKey(x => x.Id);

        builder.Ignore(x => x.DomainEvents);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.StorefrontId).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProviderType).HasColumnName("provider_type").HasMaxLength(50).HasDefaultValue("Telegram").IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.Username).HasMaxLength(100);
        builder.Property(x => x.PhotoUrl).HasMaxLength(512);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();

        builder.OwnsOne(x => x.AccountStatus, status =>
        {
            status.Property(s => s.Status)
                .HasColumnName("account_status")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(AccountStatusType.Active);
            status.Property(s => s.BlockedAt).HasColumnName("blocked_at");
            status.Property(s => s.BlockReason).HasColumnName("block_reason").HasMaxLength(512);
            status.Property(s => s.BlockedBy).HasColumnName("blocked_by").HasMaxLength(256);
            status.Property(s => s.SuspendedUntil).HasColumnName("suspended_until");
            status.Property(s => s.LastLoginAt).HasColumnName("last_login_at");
            status.Property(s => s.LastActivityAt).HasColumnName("last_activity_at");
            status.Property(s => s.TotalOrdersCount).HasColumnName("total_orders_count");
            status.Property(s => s.TotalSpent).HasColumnName("total_spent").HasColumnType("numeric(18,2)");
        });

        builder.OwnsOne(x => x.ContactVerification, verification =>
        {
            verification.Property(v => v.EmailVerified).HasColumnName("email_verified");
            verification.Property(v => v.EmailVerifiedAt).HasColumnName("email_verified_at");
            verification.Property(v => v.PhoneVerified).HasColumnName("phone_verified");
            verification.Property(v => v.PhoneVerifiedAt).HasColumnName("phone_verified_at");
        });

        builder.OwnsOne(x => x.Preferences, prefs =>
        {
            prefs.Property(p => p.EmailNotificationsEnabled).HasColumnName("email_notifications_enabled");
            prefs.Property(p => p.TelegramNotificationsEnabled).HasColumnName("telegram_notifications_enabled");
            prefs.Property(p => p.SmsNotificationsEnabled).HasColumnName("sms_notifications_enabled");
            prefs.Property(p => p.OrderStatusUpdates).HasColumnName("order_status_updates");
            prefs.Property(p => p.PromotionalOffers).HasColumnName("promotional_offers");
            prefs.Property(p => p.PreferredLanguage).HasColumnName("preferred_language").HasMaxLength(10);
            prefs.Property(p => p.PreferredCurrency).HasColumnName("preferred_currency").HasMaxLength(10);
        });

        builder.HasMany(x => x.Addresses)
            .WithOne()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.WishlistItems)
            .WithOne()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.StorefrontId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.Phone);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
