using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ExperienceMutationReceiptTests
{
    [Theory]
    [InlineData(ExperienceMutationOperation.Create)]
    [InlineData(ExperienceMutationOperation.Update)]
    [InlineData(ExperienceMutationOperation.Delete)]
    [InlineData(ExperienceMutationOperation.MapTarget)]
    public void ReceiptRetainsExactOwnerOperationKeyAndStableResult(ExperienceMutationOperation operation)
    {
        var account = Guid.NewGuid();
        var key = Guid.NewGuid();
        var entry = Guid.NewGuid();
        var now = new DateTime(2026, 10, 9, 16, 0, 0, DateTimeKind.Utc);
        var receipt = ExperienceMutationReceiptEntity.Create(account, operation, key, new string('a', 64), entry, 7, now);
        receipt.AccountId.Should().Be(account);
        receipt.Operation.Should().Be(operation);
        receipt.IdempotencyKey.Should().Be(key);
        receipt.OriginalResult.Should().Be(new ExperienceMutationResult(entry, 7));
        receipt.MatchesRequest(new string('a', 64)).Should().BeTrue();
        receipt.MatchesRequest(new string('b', 64)).Should().BeFalse();
        receipt.MatchesRequest(new string('A', 64)).Should().BeFalse();
    }

    [Fact]
    public void OrdinaryReceiptExpiresAtExactlyTwentyFourHours()
    {
        var now = new DateTime(2026, 10, 9, 16, 0, 0, DateTimeKind.Utc);
        var receipt = ExperienceMutationReceiptEntity.Create(Guid.NewGuid(), ExperienceMutationOperation.Create,
            Guid.NewGuid(), new string('a', 64), Guid.NewGuid(), 1, now);
        receipt.CreatedAtUtc.Should().Be(now);
        receipt.ExpiresAtUtc.Should().Be(now.AddHours(24));
        receipt.IsReplayable(now.AddTicks(-1)).Should().BeFalse();
        receipt.IsReplayable(now).Should().BeTrue();
        receipt.IsReplayable(now.AddHours(24).AddTicks(-1)).Should().BeTrue();
        receipt.IsReplayable(now.AddHours(24)).Should().BeFalse();
        var act = () => receipt.IsReplayable(DateTime.SpecifyKind(now, DateTimeKind.Unspecified));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InvalidReceiptIdentityResultHashAndClockAreRejected()
    {
        var account = Guid.NewGuid();
        var key = Guid.NewGuid();
        var entry = Guid.NewGuid();
        var now = new DateTime(2026, 10, 9, 16, 0, 0, DateTimeKind.Utc);
        foreach (var (owner, operation, mutationKey, hash, resultEntry, revision, clock) in
                 new (Guid, ExperienceMutationOperation, Guid, string, Guid, long, DateTime)[]
                 {
                     (Guid.Empty, ExperienceMutationOperation.Create, key, new string('a', 64), entry, 1, now),
                     (account, 0, key, new string('a', 64), entry, 1, now),
                     (account, ExperienceMutationOperation.Create, Guid.Empty, new string('a', 64), entry, 1, now),
                     (account, ExperienceMutationOperation.Create, key, new string('a', 63), entry, 1, now),
                     (account, ExperienceMutationOperation.Create, key, new string('z', 64), entry, 1, now),
                     (account, ExperienceMutationOperation.Create, key, new string('A', 64), entry, 1, now),
                     (account, ExperienceMutationOperation.Create, key, new string('a', 64), Guid.Empty, 1, now),
                     (account, ExperienceMutationOperation.Create, key, new string('a', 64), entry, 0, now),
                     (account, ExperienceMutationOperation.Create, key, new string('a', 64), entry, 1, DateTime.SpecifyKind(now, DateTimeKind.Unspecified)),
                     (account, ExperienceMutationOperation.Create, key, new string('a', 64), entry, 1, DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)),
                 })
        {
            var act = () => ExperienceMutationReceiptEntity.Create(owner, operation, mutationKey, hash, resultEntry, revision, clock);
            act.Should().Throw<ArgumentException>();
        }
    }
}
