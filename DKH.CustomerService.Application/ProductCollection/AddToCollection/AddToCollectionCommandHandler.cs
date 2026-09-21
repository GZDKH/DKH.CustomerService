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
            .Include(p => p.Experience!)
                .ThenInclude(e => e.Observations)
            .Include(p => p.Experience!)
                .ThenInclude(e => e.Tags)
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

            if (request.Experience is not null)
            {
                ReplaceExperience(existing, request.Experience, dbContext);
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

        if (request.Experience is not null)
        {
            var experience = ProductExperienceMapper.ToDomain(request.Experience, item.Id);
            item.ReplaceExperience(experience);
            if (experience is not null)
            {
                dbContext.ProductExperiences.Add(experience);
            }
        }

        dbContext.ProductCollectionItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.ToProto();
    }

    private static void ReplaceExperience(ProductCollectionItemEntity item, ProductExperienceModel model, IAppDbContext dbContext)
    {
        if (item.Experience is not null)
        {
            dbContext.ProductExperiences.Remove(item.Experience);
        }

        var experience = ProductExperienceMapper.ToDomain(model, item.Id);
        item.ReplaceExperience(experience);
        if (experience is not null)
        {
            dbContext.ProductExperiences.Add(experience);
        }
    }
}
