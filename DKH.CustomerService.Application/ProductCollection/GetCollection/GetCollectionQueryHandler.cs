using DKH.CustomerService.Application.Common;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Models.ProductCollection.v1;

namespace DKH.CustomerService.Application.ProductCollection.GetCollection;

public class GetCollectionQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<GetCollectionQuery, ProductCollectionListModel>
{
    public async Task<ProductCollectionListModel> Handle(GetCollectionQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.ProductCollectionItems
            .Where(p => p.CustomerId == request.CustomerId);

        if (request.StatusFilter.HasValue)
        {
            var domainStatus = ProductCollectionMapper.ToDomain(request.StatusFilter.Value);
            query = query.Where(p => p.Status == domainStatus);
        }

        query = query.OrderByDescending(p => p.AddedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new ProductCollectionListModel
        {
            Pagination = PaginationHelper.CreateMetadata(totalCount, page, pageSize),
        };
        result.Items.AddRange(items.Select(i => i.ToProto()));

        return result;
    }
}
