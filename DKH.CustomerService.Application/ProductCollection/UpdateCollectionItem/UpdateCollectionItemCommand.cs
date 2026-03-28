using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;

namespace DKH.CustomerService.Application.ProductCollection.UpdateCollectionItem;

public sealed record UpdateCollectionItemCommand(
    Guid ItemId,
    Guid CustomerId,
    ProductCollectionStatus? Status,
    string? Notes,
    int? Rating)
    : IRequest<ProductCollectionItemModel>;
