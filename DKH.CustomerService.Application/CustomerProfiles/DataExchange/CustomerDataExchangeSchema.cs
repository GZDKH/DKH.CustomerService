namespace DKH.CustomerService.Application.CustomerProfiles.DataExchange;

/// <summary>
///     DTO-based schema for CustomerProfile export/import.
/// </summary>
public static class CustomerDataExchangeSchema
{
    public static PlatformDataExchangeSchema Schema { get; } = PlatformDataExchangeSchemaAuto.For<CustomerDataExchangeDto>()
        .Ignore("Addresses", "WishlistItems", "ProductCollectionItems")
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
        .Collection(
            "productCollectionItems",
            c => c.ProductCollectionItems,
            item => item
                .Configure(i => i.Id, "Id", "id")
                .Configure(i => i.ProductId, "ProductId", "productId")
                .Configure(i => i.ProductSkuId, "ProductSkuId", "productSkuId")
                .Configure(i => i.Status, "Status", "status")
                .Configure(i => i.Notes, "Notes", "notes")
                .Configure(i => i.Rating, "Rating", "rating")
                .Configure(i => i.AddedAt, "AddedAt", "addedAt")
                .Configure(i => i.ExperiencedAt, "ExperiencedAt", "experiencedAt")
                .Configure(i => i.PersonalText, "PersonalText", "personalText")
                .Configure(i => i.Recommendation, "Recommendation", "recommendation")
                .Collection("observations", i => i.Observations, observation => observation
                    .Configure(o => o.DefinitionId, "DefinitionId", "definitionId")
                    .Configure(o => o.Role, "Role", "role")
                    .Configure(o => o.ValueType, "ValueType", "valueType")
                    .Configure(o => o.TextValue, "TextValue", "textValue")
                    .Configure(o => o.DecimalValue, "DecimalValue", "decimalValue")
                    .Configure(o => o.IntegerValue, "IntegerValue", "integerValue")
                    .Configure(o => o.BooleanValue, "BooleanValue", "booleanValue")
                    .Configure(o => o.UnitCode, "UnitCode", "unitCode"))
                .Collection("tags", i => i.Tags, tag => tag
                    .Configure(t => t.Value, "Value", "value")))
        .Build();
}
