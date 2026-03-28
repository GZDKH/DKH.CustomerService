using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;

namespace DKH.CustomerService.Application.ProductCollection.GetCollection;

public sealed record GetCollectionQuery(
    Guid CustomerId,
    ProductCollectionStatus? StatusFilter,
    int Page,
    int PageSize)
    : IRequest<ProductCollectionListModel>;
