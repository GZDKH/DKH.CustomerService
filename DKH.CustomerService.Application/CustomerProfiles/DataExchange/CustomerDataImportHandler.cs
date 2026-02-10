using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.CustomerService.Domain.Enums;

namespace DKH.CustomerService.Application.CustomerProfiles.DataExchange;

/// <summary>
///     Import handler for CustomerProfile entities.
///     Supports both create and update operations with nested collections (Addresses, WishlistItems).
/// </summary>
public sealed class CustomerDataImportHandler(
    IAppDbContext dbContext,
    IOptions<PlatformLocalizationOptions> localizationOptions)
    : PlatformCollectionBasedImportHandler<CustomerDataExchangeDto, string>,
        IPlatformHasProfileName
{
    private readonly Dictionary<string, Guid> _entityIds = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    protected override PlatformDataExchangeProfile<CustomerDataExchangeDto> Profile
        => CustomerDataExchangeProfile.Profile;

    /// <inheritdoc />
    protected override IReadOnlyList<string> Cultures
        => PlatformLocalizationColumnHelper.GetCultures(localizationOptions.Value);

    /// <inheritdoc />
    protected override string DefaultCulture
        => PlatformLocalizationColumnHelper.GetDefaultCulture(localizationOptions.Value, [.. Cultures]);

    /// <inheritdoc />
    public string ProfileName => CustomerDataExchangeProfileProvider.Customers;

    /// <inheritdoc />
    protected override string? GetEntityKey(CustomerDataExchangeDto dto)
        => string.IsNullOrWhiteSpace(dto.TelegramUserId) ? null : dto.TelegramUserId;

    /// <inheritdoc />
    protected override async Task<bool> EntityExistsAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.CustomerProfiles
            .AsNoTracking()
            .Where(c => c.TelegramUserId == key)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _entityIds[key] = existing.Id;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override async Task CreateEntityAsync(
        string key,
        CustomerDataExchangeDto dto,
        IReadOnlyList<PlatformTranslationData> translations,
        PlatformImportRow row,
        PlatformImportContext context,
        CancellationToken cancellationToken)
    {
        // Validate required fields
        if (dto.StorefrontId == Guid.Empty)
        {
            context.Errors.Add(new PlatformImportError(row.Raw.RowNumber, "storefrontId", "StorefrontId is required."));
            return;
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName))
        {
            context.Errors.Add(new PlatformImportError(row.Raw.RowNumber, "firstName", "FirstName is required."));
            return;
        }

        // Create CustomerProfile entity
        var customer = CustomerProfileEntity.Create(
            dto.StorefrontId,
            dto.TelegramUserId,
            dto.FirstName,
            dto.LastName,
            dto.Username,
            dto.PhotoUrl,
            dto.LanguageCode);

        // Update optional contact info
        customer.Update(
            firstName: dto.FirstName,
            lastName: dto.LastName,
            phone: dto.Phone,
            email: dto.Email,
            languageCode: dto.LanguageCode);

        // Update AccountStatus if provided
        if (!string.IsNullOrWhiteSpace(dto.AccountStatus) && Enum.TryParse<AccountStatusType>(dto.AccountStatus, out var status))
        {
            if (status == AccountStatusType.Blocked && !string.IsNullOrWhiteSpace(dto.BlockReason))
            {
                customer.AccountStatus.Block(dto.BlockReason, "Import");
            }
            else if (status == AccountStatusType.Suspended && dto.SuspendedUntil.HasValue)
            {
                customer.AccountStatus.Suspend(dto.SuspendedUntil.Value, dto.BlockReason ?? "Import", "Import");
            }
        }

        // Update order stats
        customer.AccountStatus.UpdateOrderStats(dto.TotalOrdersCount, dto.TotalSpent);

        // Update contact verification
        if (dto.EmailVerified)
        {
            customer.ContactVerification.VerifyEmail();
        }

        if (dto.PhoneVerified)
        {
            customer.ContactVerification.VerifyPhone();
        }

        // Update preferences
        customer.Preferences.UpdateNotificationChannels(
            dto.EmailNotificationsEnabled,
            dto.TelegramNotificationsEnabled,
            dto.SmsNotificationsEnabled);

        customer.Preferences.UpdateNotificationTypes(
            dto.OrderStatusUpdates,
            dto.PromotionalOffers);

        customer.Preferences.UpdateLanguage(dto.PreferredLanguage);
        customer.Preferences.UpdateCurrency(dto.PreferredCurrency);

        dbContext.CustomerProfiles.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _entityIds[key] = customer.Id;
    }

    /// <inheritdoc />
    protected override async Task UpdateEntityAsync(
        string key,
        CustomerDataExchangeDto dto,
        IReadOnlyList<PlatformTranslationData> translations,
        PlatformImportRow row,
        PlatformImportContext context,
        CancellationToken cancellationToken)
    {
        var customerId = _entityIds[key];

        var customer = await dbContext.CustomerProfiles
            .Where(c => c.Id == customerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (customer is null)
        {
            context.Errors.Add(new PlatformImportError(row.Raw.RowNumber, "telegramUserId", $"Customer with TelegramUserId '{key}' not found."));
            return;
        }

        // Update basic info
        customer.Update(
            firstName: dto.FirstName,
            lastName: dto.LastName,
            phone: dto.Phone,
            email: dto.Email,
            languageCode: dto.LanguageCode);

        // Update AccountStatus
        if (!string.IsNullOrWhiteSpace(dto.AccountStatus) && Enum.TryParse<AccountStatusType>(dto.AccountStatus, out var status))
        {
            if (status == AccountStatusType.Active && customer.AccountStatus.IsBlocked)
            {
                customer.AccountStatus.Unblock();
            }
            else if (status == AccountStatusType.Blocked && !string.IsNullOrWhiteSpace(dto.BlockReason))
            {
                customer.AccountStatus.Block(dto.BlockReason, "Import");
            }
            else if (status == AccountStatusType.Suspended && dto.SuspendedUntil.HasValue)
            {
                customer.AccountStatus.Suspend(dto.SuspendedUntil.Value, dto.BlockReason ?? "Import", "Import");
            }
        }

        // Update order stats
        customer.AccountStatus.UpdateOrderStats(dto.TotalOrdersCount, dto.TotalSpent);

        // Update contact verification
        if (dto.EmailVerified && !customer.ContactVerification.EmailVerified)
        {
            customer.ContactVerification.VerifyEmail();
        }

        if (dto.PhoneVerified && !customer.ContactVerification.PhoneVerified)
        {
            customer.ContactVerification.VerifyPhone();
        }

        // Update preferences
        customer.Preferences.UpdateNotificationChannels(
            dto.EmailNotificationsEnabled,
            dto.TelegramNotificationsEnabled,
            dto.SmsNotificationsEnabled);

        customer.Preferences.UpdateNotificationTypes(
            dto.OrderStatusUpdates,
            dto.PromotionalOffers);

        customer.Preferences.UpdateLanguage(dto.PreferredLanguage);
        customer.Preferences.UpdateCurrency(dto.PreferredCurrency);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ClearCollectionsAsync(string key, CancellationToken cancellationToken)
    {
        var customerId = _entityIds[key];

        // Remove existing addresses
        var existingAddresses = await dbContext.CustomerAddresses
            .Where(a => a.CustomerId == customerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        dbContext.CustomerAddresses.RemoveRange(existingAddresses);

        // Remove existing wishlist items
        var existingWishlistItems = await dbContext.WishlistItems
            .Where(w => w.CustomerId == customerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        dbContext.WishlistItems.RemoveRange(existingWishlistItems);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task AddCollectionItemsAsync(
        string key,
        CustomerDataExchangeDto dto,
        PlatformImportRow row,
        PlatformImportContext context,
        CancellationToken cancellationToken)
    {
        var customerId = _entityIds[key];

        // Add addresses
        foreach (var addressDto in dto.Addresses)
        {
            if (string.IsNullOrWhiteSpace(addressDto.Label) || string.IsNullOrWhiteSpace(addressDto.Country) || string.IsNullOrWhiteSpace(addressDto.City))
            {
                continue; // Skip invalid addresses
            }

            var address = CustomerAddressEntity.Create(
                customerId,
                addressDto.Label,
                addressDto.Country,
                addressDto.City,
                addressDto.Street,
                addressDto.Building,
                addressDto.Apartment,
                addressDto.PostalCode,
                addressDto.Phone,
                addressDto.IsDefault);

            dbContext.CustomerAddresses.Add(address);
        }

        // Add wishlist items
        foreach (var wishlistDto in dto.WishlistItems)
        {
            if (wishlistDto.ProductId == Guid.Empty)
            {
                continue; // Skip invalid items
            }

            var wishlistItem = WishlistItemEntity.Create(
                customerId,
                wishlistDto.ProductId,
                wishlistDto.ProductSkuId,
                wishlistDto.Note);

            dbContext.WishlistItems.Add(wishlistItem);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
