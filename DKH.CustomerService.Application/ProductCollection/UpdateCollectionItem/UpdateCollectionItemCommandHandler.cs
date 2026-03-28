using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using Grpc.Core;

namespace DKH.CustomerService.Application.ProductCollection.UpdateCollectionItem;

public class UpdateCollectionItemCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<UpdateCollectionItemCommand, ProductCollectionItemModel>
{
    public async Task<ProductCollectionItemModel> Handle(UpdateCollectionItemCommand request, CancellationToken cancellationToken)
    {
        var item = await dbContext.ProductCollectionItems
            .FirstOrDefaultAsync(p => p.Id == request.ItemId, cancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Collection item not found"));

        if (item.CustomerId != request.CustomerId)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "You do not own this collection item"));
        }

        if (request.Status.HasValue)
        {
            item.UpdateStatus(ProductCollectionMapper.ToDomain(request.Status.Value));
        }

        if (request.Notes is not null)
        {
            item.UpdateNotes(request.Notes);
        }

        if (request.Rating.HasValue)
        {
            item.UpdateRating(request.Rating.Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return item.ToProto();
    }
}
