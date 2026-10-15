using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace DKH.CustomerService.Domain.Entities.DeliveryContact;

/// <summary>Consent target selectors; they are not evidence that Delivery authorized the scope.</summary>
public sealed record DeliveryContactScope(
    Guid GroupId,
    string AllocationPublicRef,
    Guid PlanRevisionId,
    long ScopeRevision,
    Guid ChoiceRevisionId,
    DeliveryContactAuthority Duty)
{
    public const int LocalShipmentPurpose = 5;

    public void Validate()
    {
        if (GroupId == Guid.Empty || PlanRevisionId == Guid.Empty || ScopeRevision <= 0 ||
            ChoiceRevisionId == Guid.Empty || string.IsNullOrWhiteSpace(AllocationPublicRef) ||
            AllocationPublicRef.Length > 64 || AllocationPublicRef.Any(char.IsControl))
        {
            throw new ArgumentException("An exact local shipment consent target is required.");
        }

        ArgumentNullException.ThrowIfNull(Duty);
        Duty.Validate();
    }

    public string Digest()
    {
        Validate();
        string?[] fields = ["customer.delivery-contact.local-shipment.v1", GroupId.ToString("D"),
            AllocationPublicRef, PlanRevisionId.ToString("D"), ScopeRevision.ToString(CultureInfo.InvariantCulture),
            ChoiceRevisionId.ToString("D"), Duty.Authority, Duty.ResourceId.ToString("D"),
            Duty.Revision.ToString(CultureInfo.InvariantCulture), Duty.Digest];
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fields)));
    }
}
