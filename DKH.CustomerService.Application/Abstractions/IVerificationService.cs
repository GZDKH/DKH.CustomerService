namespace DKH.CustomerService.Application.Abstractions;

public interface IVerificationService
{
    Task<(bool Success, string? ErrorMessage, int ExpiresInSeconds)> SendEmailVerificationAsync(
        string email,
        string customerId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage, string? ErrorCode)> VerifyEmailCodeAsync(
        string email,
        string customerId,
        string code,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage, int ExpiresInSeconds)> SendPhoneVerificationAsync(
        string phone,
        string customerId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage, string? ErrorCode)> VerifyPhoneCodeAsync(
        string phone,
        string customerId,
        string code,
        CancellationToken cancellationToken = default);
}
