using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;
using ProtoModels = DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;

namespace DKH.CustomerService.Application.Mappers;

public static class ProductCollectionMapper
{
    public static ProtoModels.ProductCollectionItemModel ToProto(this ProductCollectionItemEntity entity)
    {
        return new ProtoModels.ProductCollectionItemModel
        {
            Id = GuidValue.FromGuid(entity.Id),
            CustomerId = GuidValue.FromGuid(entity.CustomerId),
            ProductId = GuidValue.FromGuid(entity.ProductId),
            ProductSkuId = entity.ProductSkuId.HasValue ? GuidValue.FromGuid(entity.ProductSkuId.Value) : null,
            Status = ToProto(entity.Status),
            Notes = entity.Notes,
            Rating = entity.Rating,
            AddedAt = Timestamp.FromDateTimeOffset(entity.AddedAt),
        };
    }

    public static ProtoModels.ProductCollectionStatus ToProto(ProductCollectionStatus status)
    {
        return status switch
        {
            ProductCollectionStatus.Own => ProtoModels.ProductCollectionStatus.Own,
            ProductCollectionStatus.Using => ProtoModels.ProductCollectionStatus.Using,
            ProductCollectionStatus.Finished => ProtoModels.ProductCollectionStatus.Finished,
            ProductCollectionStatus.Wishlist => ProtoModels.ProductCollectionStatus.Wishlist,
            _ => ProtoModels.ProductCollectionStatus.Unspecified,
        };
    }

    public static ProductCollectionStatus ToDomain(ProtoModels.ProductCollectionStatus status)
    {
        return status switch
        {
            ProtoModels.ProductCollectionStatus.Own => ProductCollectionStatus.Own,
            ProtoModels.ProductCollectionStatus.Using => ProductCollectionStatus.Using,
            ProtoModels.ProductCollectionStatus.Finished => ProductCollectionStatus.Finished,
            ProtoModels.ProductCollectionStatus.Wishlist => ProductCollectionStatus.Wishlist,
            _ => ProductCollectionStatus.Own,
        };
    }
}
