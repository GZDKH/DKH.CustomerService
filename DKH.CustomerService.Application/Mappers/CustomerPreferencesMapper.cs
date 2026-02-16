using DKH.CustomerService.Contracts.Customer.Models.CustomerPreferences.v1;
using DKH.Platform.Grpc.Common.Types;

namespace DKH.CustomerService.Application.Mappers;

public static class CustomerPreferencesMapper
{
    public static CustomerPreferencesModel ToContractModel(this Domain.ValueObjects.CustomerPreferences prefs, Guid customerId)
    {
        return new CustomerPreferencesModel
        {
            CustomerId = GuidValue.FromGuid(customerId),
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
