using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ProductCollection;

public sealed class ProductCollectionItemEntity : FullAuditedEntityWithKey<Guid>
{
    private ProductCollectionItemEntity()
    {
        Id = Guid.Empty;
        CustomerId = Guid.Empty;
        ProductId = Guid.Empty;
    }

    private ProductCollectionItemEntity(
        Guid customerId,
        Guid productId,
        Guid? productSkuId,
        ProductCollectionStatus status,
        string? notes,
        int? rating)
        : base(Guid.NewGuid())
    {
        CustomerId = customerId;
        ProductId = productId;
        ProductSkuId = productSkuId;
        Status = status;
        Notes = notes;
        Rating = rating;
        AddedAt = DateTimeOffset.UtcNow;
    }

    public Guid CustomerId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? ProductSkuId { get; private set; }

    public ProductCollectionStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public int? Rating { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static ProductCollectionItemEntity Create(
        Guid customerId,
        Guid productId,
        Guid? productSkuId = null,
        ProductCollectionStatus status = ProductCollectionStatus.Own,
        string? notes = null,
        int? rating = null)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5");
        }

        return new ProductCollectionItemEntity(customerId, productId, productSkuId, status, notes, rating);
    }

    public void UpdateStatus(ProductCollectionStatus status)
    {
        Status = status;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }

    public void UpdateRating(int? rating)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5");
        }

        Rating = rating;
    }
}
