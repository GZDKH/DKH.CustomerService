using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.DeliveryContact;

/// <summary>
/// An explicitly recorded current address observation, not reconstructed history.
/// Only authority metadata is retained; contact content is read from the current
/// address and must still match before any disclosure.
/// </summary>
public sealed class DeliveryContactRevisionEntity : FullAuditedEntityWithKey<Guid>
{
    private DeliveryContactRevisionEntity()
    {
        Digest = string.Empty;
        SourceDigest = string.Empty;
    }

    public Guid OwnerAccountId { get; private set; }
    public Guid StorefrontId { get; private set; }
    public Guid LegacyProfileId { get; private set; }
    public Guid AddressId { get; private set; }
    public long Revision { get; private set; }
    public string Digest { get; private set; }
    public string SourceDigest { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestDigest { get; private set; } = string.Empty;
    public DateTimeOffset ObservedAtUtc { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static DeliveryContactRevisionEntity Observe(
        Guid ownerAccountId,
        Guid storefrontId,
        CustomerAddressEntity address,
        long revision,
        Guid operationId,
        DateTimeOffset observedAtUtc, string requestDigest)
    {
        ArgumentNullException.ThrowIfNull(address);
        ValidateRequestDigest(requestDigest);
        if (ownerAccountId == Guid.Empty || storefrontId == Guid.Empty ||
            address.Id == Guid.Empty || address.CustomerId == Guid.Empty ||
            operationId == Guid.Empty || revision <= 0 || address.IsDeleted ||
            observedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A current owned contact observation is required.");
        }

        var sourceDigest = ComputeSourceDigest(ownerAccountId, storefrontId, address);
        var id = Guid.NewGuid();
        return new DeliveryContactRevisionEntity
        {
            Id = id,
            OwnerAccountId = ownerAccountId,
            StorefrontId = storefrontId,
            LegacyProfileId = address.CustomerId,
            AddressId = address.Id,
            Revision = revision,
            SourceDigest = sourceDigest,
            Digest = Hash(["customer.delivery-contact.authority.v1", id.ToString("D"),
                revision.ToString(CultureInfo.InvariantCulture), sourceDigest]),
            OperationId = operationId,
            RequestDigest = requestDigest,
            ObservedAtUtc = observedAtUtc,
        };
    }

    public bool MatchesCurrent(Guid ownerAccountId, Guid storefrontId, CustomerAddressEntity address)
        => !IsDeleted && !address.IsDeleted && OwnerAccountId == ownerAccountId &&
           StorefrontId == storefrontId && AddressId == address.Id &&
           LegacyProfileId == address.CustomerId &&
           SourceDigest == ComputeSourceDigest(ownerAccountId, storefrontId, address);

    private static string ComputeSourceDigest(
        Guid ownerAccountId,
        Guid storefrontId,
        CustomerAddressEntity address)
        => Hash(["customer.delivery-contact.source.v1", ownerAccountId.ToString("D"),
            storefrontId.ToString("D"), address.CustomerId.ToString("D"), address.Id.ToString("D"),
            address.FirstName, address.LastName, address.Country, address.City, address.Province,
            address.Street, address.Building, address.Apartment, address.PostalCode, address.Phone]);

    private static void ValidateRequestDigest(string value)
    {
        if (value is null || value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A canonical request digest is required.");
        }
    }

    private static string Hash(string?[] fields)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fields)));
}
