using DKH.CustomerService.Contracts.Customer.Models.WishlistItem.v1;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;

namespace DKH.CustomerService.Application.Mappers;

public static class WishlistMapper
{
    public static WishlistItemModel ToContractModel(this WishlistItemEntity entity)
    {
        return new WishlistItemModel
        {
            Id = GuidValue.FromGuid(entity.Id),
            CustomerId = GuidValue.FromGuid(entity.CustomerId),
            ProductId = GuidValue.FromGuid(entity.ProductId),
            ProductSkuId = entity.ProductSkuId.HasValue ? GuidValue.FromGuid(entity.ProductSkuId.Value) : null,
            AddedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(entity.AddedAt, DateTimeKind.Utc)),
            Note = entity.Note ?? string.Empty,
        };
    }
}
