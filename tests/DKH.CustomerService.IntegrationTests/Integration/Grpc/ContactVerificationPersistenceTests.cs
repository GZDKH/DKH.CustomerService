using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.ContactVerification;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Infrastructure.Persistence.Repositories;
using DKH.CustomerService.IntegrationTests.Integration.Journal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.Grpc;

// Reuse the existing isolated PostgreSQL host and migrations; no new database lifecycle.
[Trait("Category", "Integration")]
public sealed class ContactVerificationPersistenceTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ConfirmationPersistsOnlySuccessfulContactForTheResolvedStorefrontAsync(bool email, bool success)
    {
        const string contactEmail = "current@example.invalid";
        const string contactPhone = "+1-202-555-0142";
        const string code = "fixture-confirmation-code";
        var storefront = Guid.NewGuid();
        var subject = "verification:" + Guid.NewGuid().ToString("N");
        var profile = CustomerProfileEntity.Create(storefront, subject, firstName: "Fixture", phone: contactPhone, email: contactEmail);
        var foreign = CustomerProfileEntity.Create(Guid.NewGuid(), subject, firstName: "Foreign", phone: contactPhone, email: contactEmail);
        await fixture.ChangeAsync(db => db.CustomerProfiles.AddRange(profile, foreign));
        var provider = Substitute.For<IVerificationService>();
        using var lifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        string? error = success ? null : "wrong_code";
        string? errorCode = success ? null : "invalid_code";
        if (email)
        {
            provider.VerifyEmailCodeAsync(contactEmail, profile.Id.ToString(), code, token).Returns((success, error, errorCode));
        }
        else
        {
            provider.VerifyPhoneCodeAsync(contactPhone, profile.Id.ToString(), code, token).Returns((success, error, errorCode));
        }

        var response = await fixture.ReadAsync(async db =>
        {
            var useCases = new ContactVerificationUseCases(provider, new CustomerRepository(db));
            return email
                ? (await useCases.VerifyEmailAsync(storefront, subject, contactEmail, code, token)).Result
                : (await useCases.VerifyPhoneAsync(storefront, subject, contactPhone, code, token)).Result;
        });

        response.Success.Should().Be(success);
        response.ErrorCode.Should().Be(errorCode ?? string.Empty);
        var stored = await fixture.ReadAsync(db => db.CustomerProfiles.AsNoTracking()
            .Where(candidate => candidate.Id == profile.Id || candidate.Id == foreign.Id)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.ContactVerification.EmailVerified,
                candidate.ContactVerification.PhoneVerified,
            }).ToArrayAsync());
        stored.Should().HaveCount(2);
        var own = stored.Single(candidate => candidate.Id == profile.Id);
        own.EmailVerified.Should().Be(email && success);
        own.PhoneVerified.Should().Be(!email && success);
        var other = stored.Single(candidate => candidate.Id == foreign.Id);
        other.EmailVerified.Should().BeFalse();
        other.PhoneVerified.Should().BeFalse();
        if (email)
        {
            await provider.Received(1).VerifyEmailCodeAsync(contactEmail, profile.Id.ToString(), code, token);
        }
        else
        {
            await provider.Received(1).VerifyPhoneCodeAsync(contactPhone, profile.Id.ToString(), code, token);
        }
    }
}
