using DKH.CustomerService.Application.Abstractions;

namespace DKH.CustomerService.Infrastructure.Services;

/// <summary>
/// Reports contact verification as unavailable until a proof delivery provider is configured.
/// This implementation deliberately never reports that a message was sent or a code was verified.
/// </summary>
public sealed class UnavailableVerificationService : IVerificationService
{
    private const string VerificationUnavailableCode = "verification_unavailable";
    private const string EmailUnavailableMessage = "Email verification is unavailable because no proof delivery provider is configured.";
    private const string PhoneUnavailableMessage = "Phone verification is unavailable because no proof delivery provider is configured.";

    public Task<(bool Success, string? ErrorMessage, int ExpiresInSeconds)> SendEmailVerificationAsync(
        string email,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((false, (string?)EmailUnavailableMessage, 0));
    }

    public Task<(bool Success, string? ErrorMessage, string? ErrorCode)> VerifyEmailCodeAsync(
        string email,
        string customerId,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((false, (string?)EmailUnavailableMessage, (string?)VerificationUnavailableCode));
    }

    public Task<(bool Success, string? ErrorMessage, int ExpiresInSeconds)> SendPhoneVerificationAsync(
        string phone,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((false, (string?)PhoneUnavailableMessage, 0));
    }

    public Task<(bool Success, string? ErrorMessage, string? ErrorCode)> VerifyPhoneCodeAsync(
        string phone,
        string customerId,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((false, (string?)PhoneUnavailableMessage, (string?)VerificationUnavailableCode));
    }
}
