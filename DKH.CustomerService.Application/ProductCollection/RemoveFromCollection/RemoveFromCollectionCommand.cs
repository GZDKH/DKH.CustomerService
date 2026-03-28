using Google.Protobuf.WellKnownTypes;

namespace DKH.CustomerService.Application.ProductCollection.RemoveFromCollection;

public sealed record RemoveFromCollectionCommand(
    Guid ItemId,
    Guid CustomerId)
    : IRequest<Empty>;
