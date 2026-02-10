namespace DKH.CustomerService.Application.CustomerProfiles.DataExchange;

/// <summary>
///     DTO-based schema for CustomerProfile export/import.
/// </summary>
public static class CustomerDataExchangeSchema
{
    public static PlatformDataExchangeSchema Schema { get; } = PlatformDataExchangeSchemaAuto.For<CustomerDataExchangeDto>()
        .Ignore("Addresses", "WishlistItems")
        .Collection(
            "addresses",
            c => c.Addresses,
            item => item
                .Configure(a => a.Id, "Id", "id")
                .Configure(a => a.Label, "Label", "label")
                .Configure(a => a.Country, "Country", "country")
                .Configure(a => a.City, "City", "city")
                .Configure(a => a.Street, "Street", "street")
                .Configure(a => a.Building, "Building", "building")
                .Configure(a => a.Apartment, "Apartment", "apartment")
                .Configure(a => a.PostalCode, "PostalCode", "postalCode")
                .Configure(a => a.Phone, "Phone", "phone")
                .Configure(a => a.IsDefault, "IsDefault", "isDefault"))
        .Collection(
            "wishlistItems",
            c => c.WishlistItems,
            item => item
                .Configure(w => w.ProductId, "ProductId", "productId")
                .Configure(w => w.ProductSkuId, "ProductSkuId", "productSkuId")
                .Configure(w => w.AddedAt, "AddedAt", "addedAt")
                .Configure(w => w.Note, "Note", "note"))
        .Build();
}
