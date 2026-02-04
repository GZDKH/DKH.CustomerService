namespace DKH.CustomerService.Domain.ValueObjects;

public sealed class CustomerPreferences
{
    public bool EmailNotificationsEnabled { get; private set; } = true;

    public bool TelegramNotificationsEnabled { get; private set; } = true;

    public bool SmsNotificationsEnabled { get; private set; }

    public bool OrderStatusUpdates { get; private set; } = true;

    public bool PromotionalOffers { get; private set; }

    public string PreferredLanguage { get; private set; } = "en";

    public string PreferredCurrency { get; private set; } = "USD";

    public static CustomerPreferences CreateDefault() => new();

    public void UpdateNotificationChannels(bool? email, bool? telegram, bool? sms)
    {
        if (email.HasValue)
        {
            EmailNotificationsEnabled = email.Value;
        }

        if (telegram.HasValue)
        {
            TelegramNotificationsEnabled = telegram.Value;
        }

        if (sms.HasValue)
        {
            SmsNotificationsEnabled = sms.Value;
        }
    }

    public void UpdateNotificationTypes(bool? orderStatus, bool? promotional)
    {
        if (orderStatus.HasValue)
        {
            OrderStatusUpdates = orderStatus.Value;
        }

        if (promotional.HasValue)
        {
            PromotionalOffers = promotional.Value;
        }
    }

    public void UpdateLanguage(string language)
    {
        if (!string.IsNullOrWhiteSpace(language))
        {
            PreferredLanguage = language;
        }
    }

    public void UpdateCurrency(string currency)
    {
        if (!string.IsNullOrWhiteSpace(currency))
        {
            PreferredCurrency = currency;
        }
    }
}
