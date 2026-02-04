using DKH.CustomerService.Contracts.Models.V1;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using Google.Protobuf.WellKnownTypes;

namespace DKH.CustomerService.Application.Mappers;

public static class WishlistMapper
{
    public static WishlistItem ToContractModel(this WishlistItemEntity entity)
    {
        return new WishlistItem
        {
            Id = entity.Id.ToString(),
            CustomerId = entity.CustomerId.ToString(),
            ProductId = entity.ProductId.ToString(),
            ProductSkuId = entity.ProductSkuId?.ToString() ?? string.Empty,
            AddedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(entity.AddedAt, DateTimeKind.Utc)),
            Note = entity.Note ?? string.Empty,
        };
    }
}
