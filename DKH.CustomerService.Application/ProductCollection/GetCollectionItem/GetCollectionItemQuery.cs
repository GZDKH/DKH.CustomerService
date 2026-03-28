using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;

namespace DKH.CustomerService.Application.ProductCollection.GetCollectionItem;

public sealed record GetCollectionItemQuery(
    Guid CustomerId,
    Guid ProductId)
    : IRequest<ProductCollectionItemModel>;
