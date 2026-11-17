using DKH.CustomerService.Contracts.Customer.Api.ContactVerification.v1;

namespace DKH.CustomerService.Application.ContactVerification;

public interface IContactVerificationUseCases
{
    Task<InitiateEmailVerificationResponse> InitiateEmailVerificationAsync(
        Guid storefrontId, string userId, string requestedEmail, CancellationToken cancellationToken);

    Task<VerifyEmailResponse> VerifyEmailAsync(
        Guid storefrontId, string userId, string requestedEmail, string code, CancellationToken cancellationToken);

    Task<InitiatePhoneVerificationResponse> InitiatePhoneVerificationAsync(
        Guid storefrontId, string userId, string requestedPhone, CancellationToken cancellationToken);

    Task<VerifyPhoneResponse> VerifyPhoneAsync(
        Guid storefrontId, string userId, string requestedPhone, string code, CancellationToken cancellationToken);
}
