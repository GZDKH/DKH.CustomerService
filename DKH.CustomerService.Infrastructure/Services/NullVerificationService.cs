using DKH.CustomerService.Application.Abstractions;

namespace DKH.CustomerService.Infrastructure.Services;

public class NullVerificationService : IVerificationService
{
    public Task<(bool Success, string? ErrorMessage, int ExpiresInSeconds)> SendEmailVerificationAsync(
        string email,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, (string?)null, 300));
    }

    public Task<(bool Success, string? ErrorMessage, string? ErrorCode)> VerifyEmailCodeAsync(
        string email,
        string customerId,
        string code,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, (string?)null, (string?)null));
    }

    public Task<(bool Success, string? ErrorMessage, int ExpiresInSeconds)> SendPhoneVerificationAsync(
        string phone,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, (string?)null, 300));
    }

    public Task<(bool Success, string? ErrorMessage, string? ErrorCode)> VerifyPhoneCodeAsync(
        string phone,
        string customerId,
        string code,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, (string?)null, (string?)null));
    }
}
