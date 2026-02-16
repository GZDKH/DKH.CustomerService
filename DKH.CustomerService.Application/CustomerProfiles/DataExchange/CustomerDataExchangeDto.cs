using System.Text.Json.Serialization;

namespace DKH.CustomerService.Application.CustomerProfiles.DataExchange;

/// <summary>
///     DTO for CustomerProfile data exchange (import/export).
/// </summary>
public sealed class CustomerDataExchangeDto
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("storefrontId")]
    public Guid StorefrontId { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("photoUrl")]
    public string? PhotoUrl { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; } = "en";

    // AccountStatus fields
    [JsonPropertyName("accountStatus")]
    public string AccountStatus { get; set; } = "Active";

    [JsonPropertyName("blockedAt")]
    public DateTime? BlockedAt { get; set; }

    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }

    [JsonPropertyName("suspendedUntil")]
    public DateTime? SuspendedUntil { get; set; }

    [JsonPropertyName("totalOrdersCount")]
    public int TotalOrdersCount { get; set; }

    [JsonPropertyName("totalSpent")]
    public decimal TotalSpent { get; set; }

    // ContactVerification fields
    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }

    [JsonPropertyName("phoneVerified")]
    public bool PhoneVerified { get; set; }

    // CustomerPreferences fields
    [JsonPropertyName("emailNotificationsEnabled")]
    public bool EmailNotificationsEnabled { get; set; } = true;

    [JsonPropertyName("telegramNotificationsEnabled")]
    public bool TelegramNotificationsEnabled { get; set; } = true;

    [JsonPropertyName("smsNotificationsEnabled")]
    public bool SmsNotificationsEnabled { get; set; }

    [JsonPropertyName("orderStatusUpdates")]
    public bool OrderStatusUpdates { get; set; } = true;

    [JsonPropertyName("promotionalOffers")]
    public bool PromotionalOffers { get; set; }

    [JsonPropertyName("preferredLanguage")]
    public string PreferredLanguage { get; set; } = "en";

    [JsonPropertyName("preferredCurrency")]
    public string PreferredCurrency { get; set; } = "USD";

    // Collections
    [JsonPropertyName("addresses")]
    public ICollection<CustomerAddressDto> Addresses { get; set; } = [];

    [JsonPropertyName("wishlistItems")]
    public ICollection<WishlistItemDto> WishlistItems { get; set; } = [];
}

/// <summary>
///     DTO for CustomerAddress data exchange.
/// </summary>
public sealed class CustomerAddressDto
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [JsonPropertyName("building")]
    public string? Building { get; set; }

    [JsonPropertyName("apartment")]
    public string? Apartment { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}

/// <summary>
///     DTO for WishlistItem data exchange.
/// </summary>
public sealed class WishlistItemDto
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; set; }

    [JsonPropertyName("productSkuId")]
    public Guid? ProductSkuId { get; set; }

    [JsonPropertyName("addedAt")]
    public DateTime AddedAt { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
