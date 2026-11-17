using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.ContactVerification;
using DKH.CustomerService.Contracts.Customer.Models.ContactVerification.v1;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using FluentAssertions;
using Grpc.Core;
using NSubstitute;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ContactVerificationUseCasesTests
{
    private static readonly Guid StorefrontId = Guid.Parse("a90f09b6-21a4-4b8e-a726-91e1316ee402");
    private const string UserId = "verification-fixture-user";
    private const string Email = "current@example.invalid";
    private const string Phone = "+1-202-555-0142";
    private const string Code = "fixture-confirmation-code";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InitiationUsesTheStoredContactAndProfileIdentityWithCallerCancellationAsync(bool email)
    {
        var (profile, repository, provider) = CreateSut();
        using var lifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        if (email)
        {
            provider.SendEmailVerificationAsync(Email, profile.Id.ToString(), token)
                .Returns((true, null, 137));
        }
        else
        {
            provider.SendPhoneVerificationAsync(Phone, profile.Id.ToString(), token)
                .Returns((true, null, 137));
        }

        var requested = email ? "  CURRENT@EXAMPLE.INVALID  " : "  " + Phone + "  ";
        var result = await InitiateAsync(email, requested, repository, provider, token);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeEmpty();
        result.ExpiresInSeconds.Should().Be(137);
        await repository.Received(1).GetByUserIdAsync(StorefrontId, UserId, token);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
        profile.ContactVerification.EmailVerified.Should().BeFalse();
        profile.ContactVerification.PhoneVerified.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InitiationPreservesProviderFailureAndExpiryAsync(bool email)
    {
        var (profile, repository, provider) = CreateSut();
        if (email)
        {
            provider.SendEmailVerificationAsync(Email, profile.Id.ToString(), CancellationToken.None)
                .Returns((false, "retry_later", 23));
        }
        else
        {
            provider.SendPhoneVerificationAsync(Phone, profile.Id.ToString(), CancellationToken.None)
                .Returns((false, "retry_later", 23));
        }

        var result = await InitiateAsync(email, Contact(email), repository, provider, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("retry_later");
        result.ExpiresInSeconds.Should().Be(23);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulConfirmationMarksOnlyItsContactAndPersistsTheSameProfileAsync(bool email)
    {
        var (profile, repository, provider) = CreateSut();
        using var lifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        ConfirmReply(email, profile, provider, true, null, null, token);

        var result = await ConfirmAsync(email, Contact(email), repository, provider, token);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeEmpty();
        result.ErrorCode.Should().BeEmpty();
        profile.ContactVerification.EmailVerified.Should().Be(email);
        profile.ContactVerification.PhoneVerified.Should().Be(!email);
        await repository.Received(1).GetByUserIdAsync(StorefrontId, UserId, token);
        await repository.Received(1).UpdateAsync(profile, token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedConfirmationPreservesProviderErrorWithoutPersistingAsync(bool email)
    {
        var (profile, repository, provider) = CreateSut();
        ConfirmReply(email, profile, provider, false, "wrong_code", "attempts_exhausted", CancellationToken.None);

        var result = await ConfirmAsync(email, Contact(email), repository, provider, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("wrong_code");
        result.ErrorCode.Should().Be("attempts_exhausted");
        profile.ContactVerification.EmailVerified.Should().BeFalse();
        profile.ContactVerification.PhoneVerified.Should().BeFalse();
        await repository.DidNotReceive().UpdateAsync(Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task MissingProfilePreservesNotFoundAndNeverCallsTheProviderAsync(bool email, bool confirm)
    {
        var (_, repository, provider) = CreateSut();
        repository.GetByUserIdAsync(StorefrontId, UserId, Arg.Any<CancellationToken>())
            .Returns((CustomerProfileEntity?)null);
        Func<Task> operation = () => InvokeAsync(email, confirm, repository, provider, CancellationToken.None);

        var failure = await operation.Should().ThrowAsync<RpcException>();

        failure.Which.StatusCode.Should().Be(StatusCode.NotFound);
        failure.Which.Status.Detail.Should().Be("Customer profile not found");
        provider.ReceivedCalls().Should().BeEmpty();
        await repository.DidNotReceive().UpdateAsync(Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task CallerCancellationReachesTheLookupAndPreventsProviderAndPersistenceAsync(bool email, bool confirm)
    {
        var (_, repository, provider) = CreateSut();
        using var lifetime = new CancellationTokenSource();
        lifetime.Cancel();
        repository.GetByUserIdAsync(StorefrontId, UserId, lifetime.Token)
            .Returns(Task.FromCanceled<CustomerProfileEntity?>(lifetime.Token));
        Func<Task> operation = () => InvokeAsync(email, confirm, repository, provider, lifetime.Token);

        await operation.Should().ThrowAsync<OperationCanceledException>();

        provider.ReceivedCalls().Should().BeEmpty();
        await repository.DidNotReceive().UpdateAsync(Arg.Any<CustomerProfileEntity>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedPersistenceIsPropagatedInsteadOfReturningConfirmationSuccessAsync(bool email)
    {
        var (profile, repository, provider) = CreateSut();
        ConfirmReply(email, profile, provider, true, null, null, CancellationToken.None);
        var expected = new InvalidOperationException("fixture-write-failure");
        repository.UpdateAsync(profile, CancellationToken.None).Returns(Task.FromException(expected));
        Func<Task> operation = () => ConfirmAsync(email, Contact(email), repository, provider, CancellationToken.None);

        var failure = await operation.Should().ThrowAsync<InvalidOperationException>();

        failure.Which.Should().BeSameAs(expected);
    }

    private static (CustomerProfileEntity Profile, ICustomerRepository Repository, IVerificationService Provider) CreateSut()
    {
        var profile = CustomerProfileEntity.Create(StorefrontId, UserId, firstName: "Fixture", phone: Phone, email: Email);
        var repository = Substitute.For<ICustomerRepository>();
        repository.GetByUserIdAsync(StorefrontId, UserId, Arg.Any<CancellationToken>()).Returns(profile);
        return (profile, repository, Substitute.For<IVerificationService>());
    }

    private static string Contact(bool email) => email ? Email : Phone;

    private static async Task<VerificationInitiationModel> InitiateAsync(bool email, string contact,
        ICustomerRepository repository, IVerificationService provider, CancellationToken token)
        => email
            ? (await new ContactVerificationUseCases(provider, repository)
                .InitiateEmailVerificationAsync(StorefrontId, UserId, contact, token)).Result
            : (await new ContactVerificationUseCases(provider, repository)
                .InitiatePhoneVerificationAsync(StorefrontId, UserId, contact, token)).Result;

    private static async Task<VerificationResultModel> ConfirmAsync(bool email, string contact,
        ICustomerRepository repository, IVerificationService provider, CancellationToken token)
        => email
            ? (await new ContactVerificationUseCases(provider, repository)
                .VerifyEmailAsync(StorefrontId, UserId, contact, Code, token)).Result
            : (await new ContactVerificationUseCases(provider, repository)
                .VerifyPhoneAsync(StorefrontId, UserId, contact, Code, token)).Result;

    private static async Task InvokeAsync(bool email, bool confirm,
        ICustomerRepository repository, IVerificationService provider, CancellationToken token)
    {
        if (confirm)
        {
            await ConfirmAsync(email, Contact(email), repository, provider, token);
        }
        else
        {
            await InitiateAsync(email, Contact(email), repository, provider, token);
        }
    }

    private static void ConfirmReply(bool email, CustomerProfileEntity profile, IVerificationService provider,
        bool success, string? errorMessage, string? errorCode, CancellationToken token)
    {
        if (email)
        {
            provider.VerifyEmailCodeAsync(Email, profile.Id.ToString(), Code, token).Returns((success, errorMessage, errorCode));
        }
        else
        {
            provider.VerifyPhoneCodeAsync(Phone, profile.Id.ToString(), Code, token).Returns((success, errorMessage, errorCode));
        }
    }
}
