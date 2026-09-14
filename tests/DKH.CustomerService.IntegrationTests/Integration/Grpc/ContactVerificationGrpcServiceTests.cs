using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Customer.Api.ContactVerification.v1;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Infrastructure.Services;
using DKH.CustomerService.IntegrationTests.Support;
using DKH.Platform.MultiTenancy;
using FluentAssertions;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.Grpc;

public sealed class ContactVerificationGrpcServiceTests
{
    private static readonly Guid StorefrontId = Guid.Parse("30e0c528-3772-4f05-8d13-1e86b30fb569");
    private const string UserId = "profile-subject";
    private const string Email = "current@example.invalid";
    private const string Phone = "+1-202-555-0142";

    [Fact]
    public async Task InitiateEmailVerification_RejectsAnEmailOutsideTheCurrentProfileAsync()
    {
        var (service, _, verification, _) = CreateSut();

        var response = await service.InitiateEmailVerification(
            new InitiateEmailVerificationRequest
            {
                UserId = UserId,
                Email = "other@example.invalid",
            },
            new TestServerCallContext());

        response.Result.Success.Should().BeFalse();
        response.Result.ErrorMessage.Should().Contain("current profile");
        await verification.DidNotReceive().SendEmailVerificationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmail_RejectsAnEmailOutsideTheCurrentProfileWithoutPersistingAsync()
    {
        var (service, profile, verification, customerRepository) = CreateSut();

        var response = await service.VerifyEmail(
            new VerifyEmailRequest
            {
                UserId = UserId,
                Email = "other@example.invalid",
                Code = "arbitrary-code",
            },
            new TestServerCallContext());

        response.Result.Success.Should().BeFalse();
        response.Result.ErrorCode.Should().Be("contact_mismatch");
        profile.ContactVerification.EmailVerified.Should().BeFalse();
        await verification.DidNotReceive().VerifyEmailCodeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await customerRepository.DidNotReceive().UpdateAsync(
            Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiatePhoneVerification_RejectsANumberOutsideTheCurrentProfileAsync()
    {
        var (service, _, verification, _) = CreateSut();

        var response = await service.InitiatePhoneVerification(
            new InitiatePhoneVerificationRequest
            {
                UserId = UserId,
                Phone = "+1-202-555-0100",
            },
            new TestServerCallContext());

        response.Result.Success.Should().BeFalse();
        response.Result.ErrorMessage.Should().Contain("current profile");
        await verification.DidNotReceive().SendPhoneVerificationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyPhone_RejectsANumberOutsideTheCurrentProfileWithoutPersistingAsync()
    {
        var (service, profile, verification, customerRepository) = CreateSut();

        var response = await service.VerifyPhone(
            new VerifyPhoneRequest
            {
                UserId = UserId,
                Phone = "+1-202-555-0100",
                Code = "arbitrary-code",
            },
            new TestServerCallContext());

        response.Result.Success.Should().BeFalse();
        response.Result.ErrorCode.Should().Be("contact_mismatch");
        profile.ContactVerification.PhoneVerified.Should().BeFalse();
        await verification.DidNotReceive().VerifyPhoneCodeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await customerRepository.DidNotReceive().UpdateAsync(
            Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmail_WithUnavailableProviderDoesNotMarkOrPersistTheProfileAsync()
    {
        var (service, profile, _, customerRepository) = CreateSut(new UnavailableVerificationService());

        var response = await service.VerifyEmail(
            new VerifyEmailRequest
            {
                UserId = UserId,
                Email = Email,
                Code = "not-delivered",
            },
            new TestServerCallContext());

        response.Result.Success.Should().BeFalse();
        response.Result.ErrorCode.Should().Be("verification_unavailable");
        profile.ContactVerification.EmailVerified.Should().BeFalse();
        await customerRepository.DidNotReceive().UpdateAsync(
            Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyPhone_WithUnavailableProviderDoesNotMarkOrPersistTheProfileAsync()
    {
        var (service, profile, _, customerRepository) = CreateSut(new UnavailableVerificationService());

        var response = await service.VerifyPhone(
            new VerifyPhoneRequest
            {
                UserId = UserId,
                Phone = Phone,
                Code = "not-delivered",
            },
            new TestServerCallContext());

        response.Result.Success.Should().BeFalse();
        response.Result.ErrorCode.Should().Be("verification_unavailable");
        profile.ContactVerification.PhoneVerified.Should().BeFalse();
        await customerRepository.DidNotReceive().UpdateAsync(
            Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
    }

    private static (
        ContactVerificationGrpcService Service,
        CustomerProfileEntity Profile,
        IVerificationService Verification,
        ICustomerRepository CustomerRepository) CreateSut(IVerificationService? verification = null)
    {
        var profile = CustomerProfileEntity.Create(
            StorefrontId,
            UserId,
            firstName: "Test",
            phone: Phone,
            email: Email);
        var verificationService = verification ?? Substitute.For<IVerificationService>();
        var customerRepository = Substitute.For<ICustomerRepository>();
        customerRepository.GetByUserIdAsync(StorefrontId, UserId, Arg.Any<CancellationToken>())
            .Returns(profile);

        var storefrontContext = Substitute.For<IPlatformStorefrontContext>();
        storefrontContext.StorefrontId.Returns(StorefrontId);

        return (
            new ContactVerificationGrpcService(verificationService, customerRepository, storefrontContext),
            profile,
            verificationService,
            customerRepository);
    }
}
