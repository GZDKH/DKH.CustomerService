using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using Grpc.Core;

namespace DKH.CustomerService.Application.Wishlists.AddToWishlist;

public class AddToWishlistCommandHandler(ICustomerRepository repository, IAppDbContext dbContext)
    : IRequestHandler<AddToWishlistCommand, AddToWishlistResponse>
{
    public async Task<AddToWishlistResponse> Handle(AddToWishlistCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByTelegramUserIdAsync(
            request.StorefrontId,
            request.TelegramUserId,
            cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        var existing = await dbContext.WishlistItems
            .Where(w => w.CustomerId == profile.Id &&
                        w.ProductId == request.ProductId &&
                        w.ProductSkuId == request.ProductSkuId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return new AddToWishlistResponse
            {
                Item = existing.ToContractModel(),
                AlreadyExists = true,
            };
        }

        var item = WishlistItemEntity.Create(
            profile.Id,
            request.ProductId,
            request.ProductSkuId,
            request.Note);

        dbContext.WishlistItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AddToWishlistResponse
        {
            Item = item.ToContractModel(),
            AlreadyExists = false,
        };
    }
}
