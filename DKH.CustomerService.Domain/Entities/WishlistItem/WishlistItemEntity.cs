using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.WishlistItem;

public sealed class WishlistItemEntity : FullAuditedEntityWithKey<Guid>
{
    private WishlistItemEntity()
    {
        Id = Guid.Empty;
        CustomerId = Guid.Empty;
        ProductId = Guid.Empty;
        CreationTime = DateTime.UtcNow;
    }

    private WishlistItemEntity(
        Guid customerId,
        Guid productId,
        Guid? productSkuId,
        string? note)
        : base(Guid.NewGuid())
    {
        CustomerId = customerId;
        ProductId = productId;
        ProductSkuId = productSkuId;
        Note = note;
        AddedAt = DateTime.UtcNow;
        CreationTime = DateTime.UtcNow;
    }

    public Guid CustomerId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? ProductSkuId { get; private set; }

    public DateTime AddedAt { get; private set; }

    public string? Note { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static WishlistItemEntity Create(
        Guid customerId,
        Guid productId,
        Guid? productSkuId = null,
        string? note = null)
    {
        return new WishlistItemEntity(customerId, productId, productSkuId, note);
    }

    public void UpdateNote(string? note)
    {
        Note = note;
        LastModificationTime = DateTime.UtcNow;
    }
}
