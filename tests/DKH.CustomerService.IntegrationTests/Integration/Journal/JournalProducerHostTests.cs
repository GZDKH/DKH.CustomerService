using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Application.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DKH.CustomerService.IntegrationTests.Integration.Journal;

[Trait("Category", "Integration")]
public sealed class JournalProducerHostTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    [Fact]
    public async Task ThreeSameProductEntriesAndUnknownDateOnlyRetainIndependentRowsAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var product = Guid.NewGuid();
        var results = new List<ExperienceMutationResult>();
        for (var day = 1; day <= 3; day++)
        {
            results.Add(await fixture.MutateAsync(Create(Draft(product, day, "note")), token));
        }

        results.Select(result => result.EntryId).Distinct().Should().HaveCount(3);
        var unknown = await fixture.MutateAsync(Create(Draft(null, 4, null)), token);
        var header = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == unknown.EntryId));
        header.AccountId.Should().Be(account.Id);
        header.ProductId.Should().BeNull();
        header.UnknownReferenceId.Should().NotBeNull();
        header.SessionScore.Should().BeNull();
        header.LocalTimeIso.Should().BeNull();
        header.UtcOffsetMinutes.Should().BeNull();
        header.OccurredDate.Should().Be(new DateOnly(2026, 10, 4));
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(4);
    }

    [Fact]
    public async Task MatchingRetryReturnsOriginalResultAndChangedBodyConflictsAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var request = Create(Draft(Guid.NewGuid(), 1, "first"));
        var result = await fixture.MutateAsync(request, token);
        (await fixture.MutateAsync(request, token)).Should().Be(result);
        var changed = request with { Draft = request.Draft! with { Content = ExperienceContent.Create("changed", null, [], []) } };
        var error = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(changed, token));
        error.StatusCode.Should().Be(StatusCode.Aborted);
        (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
    }

    [Fact]
    public async Task ExplicitTargetMappingRetainsUnknownHistoryAndRejectsImplicitUpdateAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var saved = await fixture.MutateAsync(Create(Draft(null, 1, "unknown note")), token);
        var original = await fixture.QueryAsync(new GetJournalEntryQuery(UntrustedTemplateIdentity(), saved.EntryId), token);
        var product = Guid.NewGuid();
        var update = new MutateJournalCommand(UntrustedTemplateIdentity(), Guid.NewGuid(), ExperienceMutationOperation.Update,
            Guid.NewGuid(), saved.EntryId, 1, Draft(product, 1, "mapped note"));
        var implicitMap = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(update, token));
        implicitMap.StatusCode.Should().Be(StatusCode.InvalidArgument);
        var mapping = update with { Operation = ExperienceMutationOperation.MapTarget, IdempotencyKey = Guid.NewGuid() };
        var mapped = await fixture.MutateAsync(mapping, token);
        mapped.Revision.Should().Be(2);
        (await fixture.MutateAsync(mapping, token)).Should().Be(mapped);
        var header = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId));
        header.ProductId.Should().Be(product);
        header.UnknownReferenceId.Should().BeNull();
        var history = await fixture.QueryAsync(new ListJournalHistoryQuery(UntrustedTemplateIdentity(), saved.EntryId, 10), token);
        history.Items.Single(row => row.Revision == 1).CanonicalPayload.Should().Be(original.CanonicalPayload);
        (await fixture.ReadAsync(db => db.ExperienceUnknownReferences.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task ExpiredReceiptKeyHasExactlyTwentyFourHoursOfReplayProtectionAsync(int subMicrosecondTicks)
    {
        var clock = new JournalFixtureClock(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero).AddTicks(subMicrosecondTicks));
        var timed = new JournalHostFixture { Clock = clock };
        try
        {
            await timed.InitializeAsync();
            var account = await timed.SeedAccountAsync();
            var token = timed.Token(account.IdentitySubject);
            var request = Create(Draft(null, 1, "retained"));
            var first = await timed.MutateAsync(request, token);
            var receipt = await timed.ReadAsync(db => db.ExperienceMutationReceipts.SingleAsync(row => row.AccountId == account.Id));
            receipt.ExpiresAtUtc.Should().Be(receipt.CreatedAtUtc.AddHours(24));
            // Assert against the persisted microsecond deadline, including a
            // clock whose original time had a sub-microsecond remainder.
            clock.Now = receipt.ExpiresAtUtc - TimeSpan.FromMicroseconds(1);
            (await timed.MutateAsync(request, token)).Should().Be(first);
            clock.Now = receipt.ExpiresAtUtc;
            var second = await timed.MutateAsync(request, token);
            second.EntryId.Should().NotBe(first.EntryId);
            (await timed.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(2);
            (await timed.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        }
        finally
        {
            await timed.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateHistoryAndStaleDraftAreConflictSafeAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var draft = Draft(Guid.NewGuid(), 1, "first");
        var result = await fixture.MutateAsync(Create(draft), token);
        var before = await fixture.ReadAsync(db => db.ExperienceRevisions.SingleAsync(row => row.EntryId == result.EntryId));
        var edit = new MutateJournalCommand(UntrustedTemplateIdentity(), Guid.NewGuid(), ExperienceMutationOperation.Update,
            Guid.NewGuid(), result.EntryId, 1, draft with { Content = ExperienceContent.Create("edited", 5, [], []) });
        (await fixture.MutateAsync(edit, token)).Revision.Should().Be(2);
        var losingDraft = draft with { Content = ExperienceContent.Create("losing draft", 4, [], []) };
        var stale = edit with { IdempotencyKey = Guid.NewGuid(), Draft = losingDraft };
        var error = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(stale, token));
        error.StatusCode.Should().Be(StatusCode.Aborted);
        losingDraft.Content.PrivateNote.Should().Be("losing draft");
        (await fixture.ReadAsync(db => db.ExperienceRevisions.SingleAsync(row => row.Id == before.Id))).CanonicalPayload.Should().Be(before.CanonicalPayload);
        (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.EntryId == result.EntryId))).Should().Be(2);
    }

    [Fact]
    public async Task ForeignAndMissingResourceHaveTheSameNotFoundAndServicePurposeCannotMutateAsync()
    {
        var first = await fixture.SeedAccountAsync();
        var second = await fixture.SeedAccountAsync();
        var draft = Draft(Guid.NewGuid(), 1, "private");
        var result = await fixture.MutateAsync(Create(draft), fixture.Token(first.IdentitySubject));
        var edit = new MutateJournalCommand(UntrustedTemplateIdentity(), Guid.NewGuid(), ExperienceMutationOperation.Update,
            Guid.NewGuid(), result.EntryId, 1, draft);
        var foreign = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(edit, fixture.Token(second.IdentitySubject)));
        var missing = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(edit with { EntryId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid() }, fixture.Token(second.IdentitySubject)));
        foreign.StatusCode.Should().Be(StatusCode.NotFound);
        missing.StatusCode.Should().Be(foreign.StatusCode);
        var service = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(Create(draft), fixture.Token(first.IdentitySubject, "\"service:v1\"")));
        service.StatusCode.Should().Be(StatusCode.PermissionDenied);
        (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == second.Id))).Should().Be(0);
    }

    [Fact]
    public async Task ReceiptReplayRechecksBlockedAccountAndDeletionAppendsTombstoneAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var create = Create(Draft(Guid.NewGuid(), 1, "note"));
        var result = await fixture.MutateAsync(create, token);
        var delete = new MutateJournalCommand(UntrustedTemplateIdentity(), Guid.NewGuid(), ExperienceMutationOperation.Delete,
            Guid.NewGuid(), result.EntryId, 1, null);
        (await fixture.MutateAsync(delete, token)).Revision.Should().Be(2);
        (await fixture.ReadAsync(db => db.ExperienceEntries.IgnoreQueryFilters().SingleAsync(row => row.Id == result.EntryId))).IsDeleted.Should().BeTrue();
        (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.EntryId == result.EntryId))).Should().Be(2);
        (await fixture.MutateAsync(create, token)).Should().Be(result);
        await fixture.ChangeAsync(db => db.Entry(db.CustomerAccounts.Single(row => row.Id == account.Id))
            .Property(row => row.Status).CurrentValue = CustomerAccountStatusType.Blocked);
        var denied = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(create, token));
        denied.StatusCode.Should().Be(StatusCode.PermissionDenied);
    }

    private static MutateJournalCommand Create(JournalDraft draft) => new(UntrustedTemplateIdentity(), Guid.NewGuid(),
        ExperienceMutationOperation.Create, Guid.NewGuid(), null, null, draft);

    private static CustomerAccountIdentity UntrustedTemplateIdentity() => new("https://ignored.fixture.invalid", "browser-cannot-choose-owner");

    private static JournalDraft Draft(Guid? productId, int day, string? text) => new(
        new JournalTargetInput(productId, null, null, productId.HasValue ? null : "Private unknown", null, productId.HasValue ? "Retained Product" : null),
        ExperienceOccurrence.Create(new DateOnly(2026, 10, day), ExperienceTimePrecision.DateOnly),
        ExperienceContent.Create(text, null, [], []), ExperienceProfileSnapshotEntity.GeneralV1Id);

    [Fact]
    public async Task LifecycleLockRecheckDeniesMutationAfterConcurrentBlockAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var lockedAccount = await db.CustomerAccounts.SingleAsync(row => row.Id == account.Id);
            db.Entry(lockedAccount).Property(row => row.Status).CurrentValue = CustomerAccountStatusType.Blocked;
            await db.SaveChangesAsync();
            var blocker = ((NpgsqlConnection)db.Database.GetDbConnection()).ProcessID;
            var pending = fixture.MutateAsync(Create(Draft(Guid.NewGuid(), 1, "never committed")), token);
            var observedWait = false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                while (!pending.IsCompleted)
                {
                    var waiting = await fixture.ReadAsync(observer => observer.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE {blocker} = ANY(pg_blocking_pids(pid))").SingleAsync(timeout.Token));
                    if (waiting > 0)
                    {
                        observedWait = true;
                        break;
                    }

                    await Task.Delay(20, timeout.Token);
                }
            }
            finally
            {
                await transaction.CommitAsync();
            }

            observedWait.Should().BeTrue("the real owner request must wait on the separate lifecycle transaction");
            var error = await Assert.ThrowsAsync<RpcException>(() => pending);
            error.StatusCode.Should().Be(StatusCode.PermissionDenied);
            return true;
        });
        (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentMatchingReceiptRequestsCommitOneEntryAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var request = Create(Draft(Guid.NewGuid(), 1, "concurrent"));
        var token = fixture.Token(account.IdentitySubject);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fixture.MutateAsync(request, token)));
        results.Distinct().Should().ContainSingle();
        (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentExpectedRevisionEditsHaveOneWinnerAndOneConflictAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var draft = Draft(Guid.NewGuid(), 1, "initial");
        var saved = await fixture.MutateAsync(Create(draft), token);
        var edit = new MutateJournalCommand(UntrustedTemplateIdentity(), Guid.NewGuid(), ExperienceMutationOperation.Update,
            Guid.NewGuid(), saved.EntryId, 1, draft with { Content = ExperienceContent.Create("first contender", null, [], []) });
        var other = edit with { IdempotencyKey = Guid.NewGuid(), Draft = draft with { Content = ExperienceContent.Create("second contender", null, [], []) } };
        var attempts = new[] { fixture.MutateAsync(edit, token), fixture.MutateAsync(other, token) };
        await Assert.ThrowsAsync<RpcException>(() => Task.WhenAll(attempts));
        attempts.Count(attempt => attempt.IsCompletedSuccessfully).Should().Be(1);
        attempts.Single(attempt => attempt.IsFaulted).Exception!.InnerException.Should().BeOfType<RpcException>()
            .Which.StatusCode.Should().Be(StatusCode.Aborted);
        (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.EntryId == saved.EntryId))).Should().Be(2);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(2);
    }

    [Fact]
    public async Task FailureAfterSaveChangesRollsBackHeaderRevisionUnknownAndReceiptAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var request = Create(Draft(null, 1, "rollback"));
        fixture.Failure.Key = request.IdempotencyKey;
        try
        {
            var failure = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request, fixture.Token(account.IdentitySubject)));
            failure.StatusCode.Should().Be(StatusCode.FailedPrecondition);
            fixture.Failure.SawSavedReceipt.Should().BeTrue();
            (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
            (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
            (await fixture.ReadAsync(db => db.ExperienceUnknownReferences.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
            (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
        }
        finally
        {
            fixture.Failure.Key = null;
        }

        (await fixture.MutateAsync(request, fixture.Token(account.IdentitySubject))).Revision.Should().Be(1);
    }

    [Fact]
    public async Task MissingSharedTransactionBoundaryRejectsRealHostMutationAsync()
    {
        var withoutBoundary = new JournalHostFixture { UseTransactionBoundary = false };
        try
        {
            await withoutBoundary.InitializeAsync();
            var account = await withoutBoundary.SeedAccountAsync();
            var error = await Assert.ThrowsAsync<RpcException>(() => withoutBoundary.MutateAsync(Create(Draft(Guid.NewGuid(), 1, "rejected")),
                withoutBoundary.Token(account.IdentitySubject)));
            error.StatusCode.Should().Be(StatusCode.FailedPrecondition);
            (await withoutBoundary.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
            (await withoutBoundary.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
        }
        finally
        {
            await withoutBoundary.DisposeAsync();
        }
    }
}

public sealed class JournalFixtureClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
