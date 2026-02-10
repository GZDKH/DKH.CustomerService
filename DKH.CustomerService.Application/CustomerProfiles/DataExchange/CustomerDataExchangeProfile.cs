namespace DKH.CustomerService.Application.CustomerProfiles.DataExchange;

/// <summary>
///     Data exchange profile for CustomerProfile DTO.
/// </summary>
public static class CustomerDataExchangeProfile
{
    /// <summary>
    ///     Gets the data exchange profile for CustomerProfile import/export.
    /// </summary>
    public static PlatformDataExchangeProfile<CustomerDataExchangeDto> Profile { get; } =
        PlatformDataExchangeProfile.For<CustomerDataExchangeDto>()
            .Key(c => c.TelegramUserId)
            .Field(c => c.Id)
            .Field(c => c.StorefrontId).Required()
            .Field(c => c.FirstName).Required()
            .Field(c => c.LastName)
            .Field(c => c.Username)
            .Field(c => c.PhotoUrl)
            .Field(c => c.Phone)
            .Field(c => c.Email)
            .Field(c => c.LanguageCode).Required()
            .Field(c => c.AccountStatus).Required()
            .Field(c => c.BlockedAt)
            .Field(c => c.BlockReason)
            .Field(c => c.SuspendedUntil)
            .Field(c => c.TotalOrdersCount)
            .Field(c => c.TotalSpent)
            .Field(c => c.EmailVerified)
            .Field(c => c.PhoneVerified)
            .Field(c => c.EmailNotificationsEnabled)
            .Field(c => c.TelegramNotificationsEnabled)
            .Field(c => c.SmsNotificationsEnabled)
            .Field(c => c.OrderStatusUpdates)
            .Field(c => c.PromotionalOffers)
            .Field(c => c.PreferredLanguage).Required()
            .Field(c => c.PreferredCurrency).Required()
            .Build();
}
