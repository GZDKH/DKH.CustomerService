using DKH.CustomerService.Contracts.Models.V1;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;

namespace DKH.CustomerService.Application.Mappers;

public static class WishlistMapper
{
    public static WishlistItem ToContractModel(this WishlistItemEntity entity)
    {
        return new WishlistItem
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
