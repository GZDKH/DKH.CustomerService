using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Infrastructure;
using DKH.CustomerService.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DKH.CustomerService.IntegrationTests.Integration.Grpc;

public sealed class UnavailableVerificationServiceTests
{
    [Fact]
    public async Task ProductionInfrastructureFailsClosedWithoutAProofDeliveryProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddCustomerInfrastructure(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var verification = scope.ServiceProvider.GetRequiredService<IVerificationService>();

        verification.Should().BeOfType<UnavailableVerificationService>();

        var emailInitiation = await verification.SendEmailVerificationAsync(
            "verification-test@example.invalid",
            "isolated-test-profile");
        emailInitiation.Success.Should().BeFalse();
        emailInitiation.ExpiresInSeconds.Should().Be(0);
        emailInitiation.ErrorMessage.Should().Contain("unavailable");

        foreach (var code in new[] { "", "000000", "not-delivered" })
        {
            var emailResult = await verification.VerifyEmailCodeAsync(
                "verification-test@example.invalid",
                "isolated-test-profile",
                code);
            emailResult.Success.Should().BeFalse();
            emailResult.ErrorCode.Should().Be("verification_unavailable");
        }

        var phoneInitiation = await verification.SendPhoneVerificationAsync(
            "+1-202-555-0142",
            "isolated-test-profile");
        phoneInitiation.Success.Should().BeFalse();
        phoneInitiation.ExpiresInSeconds.Should().Be(0);
        phoneInitiation.ErrorMessage.Should().Contain("unavailable");

        foreach (var code in new[] { "", "000000", "not-delivered" })
        {
            var phoneResult = await verification.VerifyPhoneCodeAsync(
                "+1-202-555-0142",
                "isolated-test-profile",
                code);
            phoneResult.Success.Should().BeFalse();
            phoneResult.ErrorCode.Should().Be("verification_unavailable");
        }
    }
}
