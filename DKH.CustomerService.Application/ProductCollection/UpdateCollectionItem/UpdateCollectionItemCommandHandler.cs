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
            .Include(p => p.Experience!)
                .ThenInclude(e => e.Observations)
            .Include(p => p.Experience!)
                .ThenInclude(e => e.Tags)
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

        if (request.Experience is not null)
        {
            if (item.Experience is not null)
            {
                dbContext.ProductExperiences.Remove(item.Experience);
            }

            var experience = ProductExperienceMapper.ToDomain(request.Experience, item.Id);
            item.ReplaceExperience(experience);
            if (experience is not null)
            {
                dbContext.ProductExperiences.Add(experience);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return item.ToProto();
    }
}
