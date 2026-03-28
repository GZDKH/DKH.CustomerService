using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;

namespace DKH.CustomerService.Application.ProductCollection.AddToCollection;

public sealed record AddToCollectionCommand(
    Guid CustomerId,
    Guid ProductId,
    Guid? ProductSkuId,
    ProductCollectionStatus Status,
    string? Notes,
    int? Rating)
    : IRequest<ProductCollectionItemModel>;
