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
        => string.IsNullOrWhiteSpace(dto.UserId) ? null : dto.UserId;

    /// <inheritdoc />
    protected override async Task<bool> EntityExistsAsync(
        string key,
        CancellationToken cancellationToken)
    {
        // Check in-memory cache first to handle duplicate keys within the same batch
        if (_entityIds.ContainsKey(key))
        {
            return true;
        }

        var existing = await dbContext.CustomerProfiles
            .AsNoTracking()
            .Where(c => c.UserId == key)
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
    protected override Task CreateEntityAsync(
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
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName))
        {
            context.Errors.Add(new PlatformImportError(row.Raw.RowNumber, "firstName", "FirstName is required."));
            return Task.CompletedTask;
        }

        // Create CustomerProfile entity
        var customer = CustomerProfileEntity.Create(
            dto.StorefrontId,
            dto.UserId,
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

        ApplyAccountStatus(customer, dto, row, context);
        customer.AccountStatus.UpdateOrderStats(dto.TotalOrdersCount, dto.TotalSpent);
        ApplyContactVerification(customer, dto);
        ApplyPreferences(customer, dto);

        dbContext.CustomerProfiles.Add(customer);
        _entityIds[key] = customer.Id;

        return Task.CompletedTask;
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
            context.Errors.Add(new PlatformImportError(row.Raw.RowNumber, "userId", $"Customer with UserId '{key}' not found."));
            return;
        }

        // Update basic info
        customer.Update(
            firstName: dto.FirstName,
            lastName: dto.LastName,
            phone: dto.Phone,
            email: dto.Email,
            languageCode: dto.LanguageCode);

        ApplyAccountStatus(customer, dto, row, context);
        customer.AccountStatus.UpdateOrderStats(dto.TotalOrdersCount, dto.TotalSpent);
        ApplyContactVerification(customer, dto);
        ApplyPreferences(customer, dto);
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
    }

    /// <inheritdoc />
    protected override Task AddCollectionItemsAsync(
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
            if (string.IsNullOrWhiteSpace(addressDto.Label)
                || string.IsNullOrWhiteSpace(addressDto.Country)
                || string.IsNullOrWhiteSpace(addressDto.City))
            {
                context.Errors.Add(new PlatformImportError(
                    row.Raw.RowNumber,
                    "address",
                    $"Address skipped: Label, Country, and City are required (got Label='{addressDto.Label}', Country='{addressDto.Country}', City='{addressDto.City}')."));
                continue;
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
                context.Errors.Add(new PlatformImportError(
                    row.Raw.RowNumber,
                    "wishlistItem",
                    "WishlistItem skipped: ProductId is required."));
                continue;
            }

            var wishlistItem = WishlistItemEntity.Create(
                customerId,
                wishlistDto.ProductId,
                wishlistDto.ProductSkuId,
                wishlistDto.Note);

            dbContext.WishlistItems.Add(wishlistItem);
        }

        return Task.CompletedTask;
    }

    private static void ApplyAccountStatus(
        CustomerProfileEntity customer,
        CustomerDataExchangeDto dto,
        PlatformImportRow row,
        PlatformImportContext context)
    {
        if (string.IsNullOrWhiteSpace(dto.AccountStatus))
        {
            return;
        }

        if (!Enum.TryParse<AccountStatusType>(dto.AccountStatus, ignoreCase: true, out var status))
        {
            context.Errors.Add(new PlatformImportError(
                row.Raw.RowNumber,
                "accountStatus",
                $"Invalid AccountStatus value '{dto.AccountStatus}'. Expected: Active, Blocked, Suspended."));
            return;
        }

        switch (status)
        {
            case AccountStatusType.Active when customer.AccountStatus.IsBlocked || customer.AccountStatus.IsSuspended:
                customer.AccountStatus.Unblock();
                break;

            case AccountStatusType.Blocked:
                customer.AccountStatus.Block(dto.BlockReason ?? "Imported as blocked", "Import");
                break;

            case AccountStatusType.Suspended:
                customer.AccountStatus.Suspend(
                    dto.SuspendedUntil ?? DateTime.UtcNow.AddDays(30),
                    dto.BlockReason ?? "Imported as suspended",
                    "Import");
                break;
        }
    }

    private static void ApplyContactVerification(CustomerProfileEntity customer, CustomerDataExchangeDto dto)
    {
        if (dto.EmailVerified && !customer.ContactVerification.EmailVerified)
        {
            customer.ContactVerification.VerifyEmail();
        }
        else if (!dto.EmailVerified && customer.ContactVerification.EmailVerified)
        {
            customer.ContactVerification.ResetEmailVerification();
        }

        if (dto.PhoneVerified && !customer.ContactVerification.PhoneVerified)
        {
            customer.ContactVerification.VerifyPhone();
        }
        else if (!dto.PhoneVerified && customer.ContactVerification.PhoneVerified)
        {
            customer.ContactVerification.ResetPhoneVerification();
        }
    }

    private static void ApplyPreferences(CustomerProfileEntity customer, CustomerDataExchangeDto dto)
    {
        customer.Preferences.UpdateNotificationChannels(
            dto.EmailNotificationsEnabled,
            dto.TelegramNotificationsEnabled,
            dto.SmsNotificationsEnabled);

        customer.Preferences.UpdateNotificationTypes(
            dto.OrderStatusUpdates,
            dto.PromotionalOffers);

        if (!string.IsNullOrWhiteSpace(dto.PreferredLanguage))
        {
            customer.Preferences.UpdateLanguage(dto.PreferredLanguage);
        }

        if (!string.IsNullOrWhiteSpace(dto.PreferredCurrency))
        {
            customer.Preferences.UpdateCurrency(dto.PreferredCurrency);
        }
    }
}
