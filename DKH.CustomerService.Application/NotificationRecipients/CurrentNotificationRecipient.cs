using System.Net.Mail;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Domain.Enums;

namespace DKH.CustomerService.Application.NotificationRecipients;

public enum NotificationEmailEligibility
{
    Available = 1,
    Disabled = 2,
    UnverifiedProfileOverride = 3,
    VerifiedContactUnavailable = 4
}

/// <summary>Current private DATA; canonical account identity is not a profile or user UUID.
/// No field delegates the buyer or establishes later dispatch/provider success.</summary>
public sealed record NotificationRecipientSnapshot(Guid CustomerProfileId, Guid StorefrontId,
    string CanonicalIssuer, string CanonicalSubject, string Email, NotificationEmailEligibility EmailEligibility,
    DateTimeOffset? EmailVerifiedAt, bool EmailNotificationsEnabled, bool TelegramNotificationsEnabled,
    bool SmsNotificationsEnabled, string PreferredLanguage, DateTimeOffset ObservedAt, DateTimeOffset ReadValidUntil);

public sealed class NotificationRecipientChangedException() : Exception("Current notification recipient source changed or expired.");

/// <summary>Read-only owner projection. Callers supply their independent actual current service fence.
/// Existing active binding/verified aliases are reused; no customer is provisioned or reconciled.</summary>
public sealed class CurrentNotificationRecipient(IAppDbContext db, CurrentActiveProfileBinding bindings, TimeProvider clock)
{
    public async Task<NotificationRecipientSnapshot> ReadAsync(CustomerAccountIdentity actor, Guid storefrontId, Guid profileId,
        DateTimeOffset callerValidUntil, Func<CancellationToken, Task> requireCurrentCaller, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(requireCurrentCaller);
        if (storefrontId == Guid.Empty || profileId == Guid.Empty)
        {
            throw new ArgumentException("An exact storefront and profile are required.");
        }

        var until = clock.GetUtcNow().AddSeconds(5);
        if (callerValidUntil < until)
        {
            until = callerValidUntil;
        }

        async Task Current()
        {
            ct.ThrowIfCancellationRequested();
            await requireCurrentCaller(ct);
            if (clock.GetUtcNow() >= until)
            {
                throw new NotificationRecipientChangedException();
            }
        }

        await Current();
        async Task Binding()
        {
            var binding = await bindings.GetAsync(actor, storefrontId, ct);
            await Current();
            if (binding.CustomerProfileId != profileId || binding.StorefrontId != storefrontId)
            {
                throw new CustomerAccountNotFoundException("Personal resource was not found.");
            }
        }

        async Task<ProfileData> Profile()
        {
            var value = await db.CustomerProfiles.IgnoreQueryFilters().AsNoTracking()
                .Where(profile => profile.Id == profileId && profile.StorefrontId == storefrontId && !profile.IsDeleted &&
                    profile.CustomerAccountId != null && profile.AccountReconciliationStatus == CustomerAccountReconciliationStatusType.Linked &&
                    profile.AccountStatus.Status == AccountStatusType.Active)
                .Select(profile => new ProfileData(profile.CustomerAccountId!.Value, profile.Email ?? string.Empty,
                    profile.Preferences.EmailNotificationsEnabled, profile.Preferences.TelegramNotificationsEnabled,
                    profile.Preferences.SmsNotificationsEnabled, profile.Preferences.PreferredLanguage, profile.LastModificationTime))
                .SingleOrDefaultAsync(ct);
            await Current();
            return value ?? throw new CustomerAccountNotFoundException("Personal resource was not found.");
        }

        async Task<AccountData> Account(Guid id)
        {
            var value = await db.CustomerAccounts.IgnoreQueryFilters().AsNoTracking()
                .Where(account => account.Id == id && !account.IsDeleted && account.Status == CustomerAccountStatusType.Active)
                .Select(account => new AccountData(account.IdentityIssuer, account.IdentitySubject, account.VerifiedEmail,
                    account.EmailVerifiedAt, account.LastModificationTime)).SingleOrDefaultAsync(ct);
            await Current();
            return value ?? throw new CustomerAccountNotFoundException("Personal resource was not found.");
        }

        await Binding();
        var profile = await Profile();
        var account = await Account(profile.AccountId);
        await Binding();
        if (await Profile() != profile || await Account(profile.AccountId) != account)
        {
            throw new NotificationRecipientChangedException();
        }

        await Current();
        var at = clock.GetUtcNow();
        var eligibility = !profile.EmailEnabled ? NotificationEmailEligibility.Disabled :
            !string.IsNullOrEmpty(profile.Email) && !profile.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase)
                ? NotificationEmailEligibility.UnverifiedProfileOverride :
            account.VerifiedAt <= DateTime.UnixEpoch || account.VerifiedAt > at.UtcDateTime ||
                account.Email.Length > 256 || account.Email.Any(char.IsControl) ||
                !MailAddress.TryCreate(account.Email, out var parsed) || parsed.Address != account.Email
                ? NotificationEmailEligibility.VerifiedContactUnavailable : NotificationEmailEligibility.Available;
        return new(profileId, storefrontId, account.Issuer, account.Subject,
            eligibility == NotificationEmailEligibility.Available ? account.Email : string.Empty, eligibility,
            eligibility == NotificationEmailEligibility.Available ? new DateTimeOffset(DateTime.SpecifyKind(account.VerifiedAt, DateTimeKind.Utc)) : null,
            profile.EmailEnabled, profile.TelegramEnabled, profile.SmsEnabled, profile.Locale, at, until);
    }

    private sealed record ProfileData(Guid AccountId, string Email, bool EmailEnabled, bool TelegramEnabled, bool SmsEnabled,
        string Locale, DateTime? ModifiedAt);
    private sealed record AccountData(string Issuer, string Subject, string Email, DateTime VerifiedAt, DateTime? ModifiedAt);
}
