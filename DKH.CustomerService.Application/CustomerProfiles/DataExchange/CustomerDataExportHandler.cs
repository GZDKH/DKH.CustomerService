using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Enums;

namespace DKH.CustomerService.Application.CustomerProfiles.DataExchange;

/// <summary>
///     Export handler for CustomerProfile entities using DTO-based approach.
/// </summary>
public sealed class CustomerDataExportHandler(
    IAppDbContext dbContext,
    IOptions<PlatformLocalizationOptions> localizationOptions)
    : AppDataExportHandlerBase<CustomerProfileEntity, CustomerDataExchangeDto>(dbContext, localizationOptions)
{
    /// <inheritdoc />
    protected override PlatformDataExchangeProfile<CustomerDataExchangeDto> Profile
        => CustomerDataExchangeProfile.Profile;

    /// <inheritdoc />
    protected override PlatformDataExchangeSchema Schema => CustomerDataExchangeSchema.Schema;

    /// <inheritdoc />
    protected override IReadOnlyList<string> PrimaryCollectionNames
        => ["addresses", "wishlistItems"];

    /// <inheritdoc />
    public override string ProfileName => CustomerDataExchangeProfileProvider.Customers;

    /// <inheritdoc />
    protected override CustomerDataExchangeDto MapToDto(CustomerProfileEntity entity) => new()
    {
        Id = entity.Id,
        StorefrontId = entity.StorefrontId,
        UserId = entity.UserId,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        Username = entity.Username,
        PhotoUrl = entity.PhotoUrl,
        Phone = entity.Phone,
        Email = entity.Email,
        LanguageCode = entity.LanguageCode,

        // AccountStatus fields (flattened)
        AccountStatus = entity.AccountStatus.Status.ToString(),
        BlockedAt = entity.AccountStatus.BlockedAt,
        BlockReason = entity.AccountStatus.BlockReason,
        SuspendedUntil = entity.AccountStatus.SuspendedUntil,
        TotalOrdersCount = entity.AccountStatus.TotalOrdersCount,
        TotalSpent = entity.AccountStatus.TotalSpent,

        // ContactVerification fields (flattened)
        EmailVerified = entity.ContactVerification.EmailVerified,
        PhoneVerified = entity.ContactVerification.PhoneVerified,

        // CustomerPreferences fields (flattened)
        EmailNotificationsEnabled = entity.Preferences.EmailNotificationsEnabled,
        TelegramNotificationsEnabled = entity.Preferences.TelegramNotificationsEnabled,
        SmsNotificationsEnabled = entity.Preferences.SmsNotificationsEnabled,
        OrderStatusUpdates = entity.Preferences.OrderStatusUpdates,
        PromotionalOffers = entity.Preferences.PromotionalOffers,
        PreferredLanguage = entity.Preferences.PreferredLanguage,
        PreferredCurrency = entity.Preferences.PreferredCurrency,

        // Collections
        Addresses =
        [
            .. entity.Addresses.Select(a => new CustomerAddressDto
            {
                Id = a.Id,
                Label = a.Label,
                Country = a.Country,
                City = a.City,
                Street = a.Street,
                Building = a.Building,
                Apartment = a.Apartment,
                PostalCode = a.PostalCode,
                Phone = a.Phone,
                IsDefault = a.IsDefault,
            }),
        ],
        WishlistItems =
        [
            .. entity.WishlistItems.Select(w => new WishlistItemDto
            {
                ProductId = w.ProductId,
                ProductSkuId = w.ProductSkuId,
                AddedAt = w.AddedAt,
                Note = w.Note,
            }),
        ],
    };

    /// <inheritdoc />
    protected override IQueryable<CustomerProfileEntity> BuildQuery(PlatformExportContext context)
    {
        var search = context.GetSearch();

        var query = DbContext.CustomerProfiles
            .Include(c => c.Addresses)
            .Include(c => c.WishlistItems)
            .AsNoTracking();

        // Filter by storefrontId if provided
        if (context.Parameters.TryGetValue("storefrontId", out var storefrontIdValue)
            && storefrontIdValue is Guid storefrontId)
        {
            query = query.Where(c => c.StorefrontId == storefrontId);
        }

        // Filter by account status
        var status = context.GetStringParameter("status");
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AccountStatusType>(status, ignoreCase: true, out var statusType))
        {
            query = query.Where(c => c.AccountStatus.Status == statusType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                EF.Functions.Like(c.UserId, $"%{search}%")
                || EF.Functions.Like(c.FirstName, $"%{search}%")
                || (c.LastName != null && EF.Functions.Like(c.LastName, $"%{search}%"))
                || (c.Username != null && EF.Functions.Like(c.Username, $"%{search}%"))
                || (c.Phone != null && EF.Functions.Like(c.Phone, $"%{search}%"))
                || (c.Email != null && EF.Functions.Like(c.Email, $"%{search}%")));
        }

        query = query.OrderBy(c => c.CreationTime).ThenBy(c => c.UserId);

        return ApplyPaging(query, context);
    }
}
