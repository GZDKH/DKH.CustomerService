using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;
using DKH.CustomerService.Domain.Entities.ProductCollection;

namespace DKH.CustomerService.Application.ProductCollection.AddToCollection;

public class AddToCollectionCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<AddToCollectionCommand, ProductCollectionItemModel>
{
    public async Task<ProductCollectionItemModel> Handle(AddToCollectionCommand request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ProductCollectionItems
            .Where(p => p.CustomerId == request.CustomerId &&
                        p.ProductId == request.ProductId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            existing.UpdateStatus(ProductCollectionMapper.ToDomain(request.Status));
            if (request.Notes is not null)
            {
                existing.UpdateNotes(request.Notes);
            }

            if (request.Rating.HasValue)
            {
                existing.UpdateRating(request.Rating.Value);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return existing.ToProto();
        }

        var item = ProductCollectionItemEntity.Create(
            request.CustomerId,
            request.ProductId,
            request.ProductSkuId,
            ProductCollectionMapper.ToDomain(request.Status),
            request.Notes,
            request.Rating);

        dbContext.ProductCollectionItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.ToProto();
    }
}
