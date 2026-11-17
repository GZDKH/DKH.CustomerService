using DKH.CustomerService.Contracts.Customer.Api.ContactVerification.v1;
using DKH.CustomerService.Contracts.Customer.Models.ContactVerification.v1;
using Grpc.Core;

namespace DKH.CustomerService.Application.ContactVerification;

public sealed class ContactVerificationUseCases(
    IVerificationService verificationService,
    ICustomerRepository customerRepository) : IContactVerificationUseCases
{
    private const string ContactMismatchErrorCode = "contact_mismatch";
    private const string EmailMismatchErrorMessage = "Email must match the current profile email.";
    private const string PhoneMismatchErrorMessage = "Phone must match the current profile phone.";

    public async Task<InitiateEmailVerificationResponse> InitiateEmailVerificationAsync(
        Guid storefrontId, string userId, string requestedEmail, CancellationToken cancellationToken)
    {
        var profile = await customerRepository.GetByUserIdAsync(storefrontId, userId, cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));
        var email = MatchContact(requestedEmail, profile.Email, StringComparison.OrdinalIgnoreCase);

        if (email is null)
        {
            return new InitiateEmailVerificationResponse
            {
                Result = new VerificationInitiationModel
                {
                    Success = false,
                    ErrorMessage = EmailMismatchErrorMessage,
                    ExpiresInSeconds = 0,
                },
            };
        }

        var (success, errorMessage, expiresIn) = await verificationService.SendEmailVerificationAsync(
            email,
            profile.Id.ToString(),
            cancellationToken);

        return new InitiateEmailVerificationResponse
        {
            Result = new VerificationInitiationModel
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ExpiresInSeconds = expiresIn,
            },
        };
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(
        Guid storefrontId, string userId, string requestedEmail, string code, CancellationToken cancellationToken)
    {
        var profile = await customerRepository.GetByUserIdAsync(storefrontId, userId, cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));
        var email = MatchContact(requestedEmail, profile.Email, StringComparison.OrdinalIgnoreCase);

        if (email is null)
        {
            return new VerifyEmailResponse
            {
                Result = new VerificationResultModel
                {
                    Success = false,
                    ErrorMessage = EmailMismatchErrorMessage,
                    ErrorCode = ContactMismatchErrorCode,
                },
            };
        }

        var (success, errorMessage, errorCode) = await verificationService.VerifyEmailCodeAsync(
            email,
            profile.Id.ToString(),
            code,
            cancellationToken);

        if (success)
        {
            profile.ContactVerification.VerifyEmail();
            await customerRepository.UpdateAsync(profile, cancellationToken);
        }

        return new VerifyEmailResponse
        {
            Result = new VerificationResultModel
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ErrorCode = errorCode ?? string.Empty,
            },
        };
    }

    public async Task<InitiatePhoneVerificationResponse> InitiatePhoneVerificationAsync(
        Guid storefrontId, string userId, string requestedPhone, CancellationToken cancellationToken)
    {
        var profile = await customerRepository.GetByUserIdAsync(storefrontId, userId, cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));
        var phone = MatchContact(requestedPhone, profile.Phone, StringComparison.Ordinal);

        if (phone is null)
        {
            return new InitiatePhoneVerificationResponse
            {
                Result = new VerificationInitiationModel
                {
                    Success = false,
                    ErrorMessage = PhoneMismatchErrorMessage,
                    ExpiresInSeconds = 0,
                },
            };
        }

        var (success, errorMessage, expiresIn) = await verificationService.SendPhoneVerificationAsync(
            phone,
            profile.Id.ToString(),
            cancellationToken);

        return new InitiatePhoneVerificationResponse
        {
            Result = new VerificationInitiationModel
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ExpiresInSeconds = expiresIn,
            },
        };
    }

    public async Task<VerifyPhoneResponse> VerifyPhoneAsync(
        Guid storefrontId, string userId, string requestedPhone, string code, CancellationToken cancellationToken)
    {
        var profile = await customerRepository.GetByUserIdAsync(storefrontId, userId, cancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));
        var phone = MatchContact(requestedPhone, profile.Phone, StringComparison.Ordinal);

        if (phone is null)
        {
            return new VerifyPhoneResponse
            {
                Result = new VerificationResultModel
                {
                    Success = false,
                    ErrorMessage = PhoneMismatchErrorMessage,
                    ErrorCode = ContactMismatchErrorCode,
                },
            };
        }

        var (success, errorMessage, errorCode) = await verificationService.VerifyPhoneCodeAsync(
            phone,
            profile.Id.ToString(),
            code,
            cancellationToken);

        if (success)
        {
            profile.ContactVerification.VerifyPhone();
            await customerRepository.UpdateAsync(profile, cancellationToken);
        }

        return new VerifyPhoneResponse
        {
            Result = new VerificationResultModel
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ErrorCode = errorCode ?? string.Empty,
            },
        };
    }

    private static string? MatchContact(string requested, string? current, StringComparison comparison)
    {
        if (string.IsNullOrWhiteSpace(requested) || string.IsNullOrWhiteSpace(current))
        {
            return null;
        }

        return string.Equals(requested.Trim(), current.Trim(), comparison) ? current : null;
    }
}
