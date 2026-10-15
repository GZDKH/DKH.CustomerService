namespace DKH.CustomerService.Domain.Entities.DeliveryContact;

public sealed record DeliveryContactAuthority(string Authority, Guid ResourceId, long Revision, string Digest)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Authority) || Authority.Length > 128 ||
            ResourceId == Guid.Empty || Revision <= 0 || Digest is null || Digest.Length != 64 ||
            Digest.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("An exact contact or duty authority is required.");
        }
    }
}
