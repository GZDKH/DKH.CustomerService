using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace DKH.CustomerService.Application.ProductCollection.RemoveFromCollection;

public class RemoveFromCollectionCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<RemoveFromCollectionCommand, Empty>
{
    public async Task<Empty> Handle(RemoveFromCollectionCommand request, CancellationToken cancellationToken)
    {
        var item = await dbContext.ProductCollectionItems
            .FirstOrDefaultAsync(p => p.Id == request.ItemId, cancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Collection item not found"));

        if (item.CustomerId != request.CustomerId)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "You do not own this collection item"));
        }

        dbContext.ProductCollectionItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new Empty();
    }
}
