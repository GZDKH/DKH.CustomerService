namespace DKH.CustomerService.Domain.ValueObjects;

public sealed class ContactVerification
{
    public bool EmailVerified { get; private set; }

    public DateTime? EmailVerifiedAt { get; private set; }

    public bool PhoneVerified { get; private set; }

    public DateTime? PhoneVerifiedAt { get; private set; }

    public static ContactVerification CreateNew() => new();

    public void VerifyEmail()
    {
        EmailVerified = true;
        EmailVerifiedAt = DateTime.UtcNow;
    }

    public void VerifyPhone()
    {
        PhoneVerified = true;
        PhoneVerifiedAt = DateTime.UtcNow;
    }

    public void ResetEmailVerification()
    {
        EmailVerified = false;
        EmailVerifiedAt = null;
    }

    public void ResetPhoneVerification()
    {
        PhoneVerified = false;
        PhoneVerifiedAt = null;
    }
}
