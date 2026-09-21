using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using Grpc.Core;

namespace DKH.CustomerService.Application.ProductCollection.GetCollectionItem;

public class GetCollectionItemQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<GetCollectionItemQuery, ProductCollectionItemModel>
{
    public async Task<ProductCollectionItemModel> Handle(GetCollectionItemQuery request, CancellationToken cancellationToken)
    {
        var item = await dbContext.ProductCollectionItems
            .Include(p => p.Experience!)
                .ThenInclude(e => e.Observations)
            .Include(p => p.Experience!)
                .ThenInclude(e => e.Tags)
            .Where(p => p.CustomerId == request.CustomerId &&
                        p.ProductId == request.ProductId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Product not found in collection"));

        return item.ToProto();
    }
}
