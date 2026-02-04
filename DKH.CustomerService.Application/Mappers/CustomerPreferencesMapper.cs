using DKH.CustomerService.Contracts.Models.V1;

namespace DKH.CustomerService.Application.Mappers;

public static class CustomerPreferencesMapper
{
    public static CustomerPreferences ToContractModel(this Domain.ValueObjects.CustomerPreferences prefs, string customerId)
    {
        return new CustomerPreferences
        {
            CustomerId = customerId,
            EmailNotificationsEnabled = prefs.EmailNotificationsEnabled,
            TelegramNotificationsEnabled = prefs.TelegramNotificationsEnabled,
            SmsNotificationsEnabled = prefs.SmsNotificationsEnabled,
            OrderStatusUpdates = prefs.OrderStatusUpdates,
            PromotionalOffers = prefs.PromotionalOffers,
            PreferredLanguage = prefs.PreferredLanguage,
            PreferredCurrency = prefs.PreferredCurrency,
        };
    }
}
