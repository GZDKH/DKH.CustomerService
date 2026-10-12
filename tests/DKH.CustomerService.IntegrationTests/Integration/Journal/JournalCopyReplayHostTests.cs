using System.Text.Json;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Application.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Domain.ValueObjects;
using DKH.CustomerService.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.IntegrationTests.Integration.Journal;

[Trait("Category", "Integration")]
public sealed class JournalCopyReplayHostTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    private static readonly CustomerAccountIdentity Identity = new("ignored", "browser-cannot-select-owner");
    private static ExperienceOccurrence Occurrence(int day) => ExperienceOccurrence.Create(new DateOnly(2026, 10, day), ExperienceTimePrecision.DateOnly);
    private static JournalDraft Draft(Guid? product = null) => new(
        new JournalTargetInput(product, null, null, product.HasValue ? null : "Private source", null, product.HasValue ? "Retained product" : null),
        Occurrence(1), ExperienceContent.Create("original private note", 3, ["tag"], [], "pack", "lot"), ExperienceProfileSnapshotEntity.GeneralV1Id);
    private static MutateJournalCommand Create(JournalDraft draft) => new(Identity, Guid.NewGuid(), ExperienceMutationOperation.Create,
        Guid.NewGuid(), null, null, draft);
    private static MutateJournalCommand Copy(ExperienceMutationResult source) => new(Identity, Guid.NewGuid(), ExperienceMutationOperation.Create,
        Guid.NewGuid(), null, null, null, new JournalCopyIntent(source.EntryId, source.Revision, Occurrence(12), JournalCopyTargetChoice.Retain));
    private static MutateJournalCommand Edit(ExperienceMutationResult entry, JournalDraft draft) => new(Identity, Guid.NewGuid(), ExperienceMutationOperation.Update,
        Guid.NewGuid(), entry.EntryId, entry.Revision, draft);
    private static MutateJournalCommand Delete(ExperienceMutationResult entry) => new(Identity, Guid.NewGuid(), ExperienceMutationOperation.Delete,
        Guid.NewGuid(), entry.EntryId, entry.Revision, null);
    private Task<JournalRevisionView> Saved(ExperienceMutationResult result, string token) => fixture.QueryAsync(
        new GetJournalEntryQuery(Identity, result.EntryId, result.Revision), token);

    [Fact]
    public async Task ConcurrentCopyRetriesAndSourceEditDeleteRetainOneOriginalDestinationAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var sourceDraft = Draft();
        var source = await fixture.MutateAsync(Create(sourceDraft), token);
        var request = Copy(source);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => fixture.MutateAsync(request, token)));
        results.Distinct().Should().ContainSingle();
        var destination = results[0];
        var body = await Saved(destination, token);
        using var payload = JsonDocument.Parse(body.CanonicalPayload);
        payload.RootElement.GetProperty("privateNote").GetString().Should().Be("original private note");
        payload.RootElement.GetProperty("occurrence").GetProperty("occurredDate").GetString().Should().Be("2026-10-12");
        var sourceHeader = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == source.EntryId));
        var edited = await fixture.MutateAsync(Edit(source, sourceDraft with
        {
            Target = new JournalTargetInput(null, null, sourceHeader.UnknownReferenceId, null, null, null),
            Content = ExperienceContent.Create("source edited", 5, [], [])
        }), token);
        (await fixture.MutateAsync(request, token)).Should().Be(destination);
        // A new key selecting the old immutable source revision also copies the old body.
        var oldRevisionCopy = await fixture.MutateAsync(request with { IdempotencyKey = Guid.NewGuid() }, token);
        (await Saved(oldRevisionCopy, token)).CanonicalPayload.Should().Be(body.CanonicalPayload);
        await fixture.MutateAsync(Delete(edited), token);
        (await fixture.MutateAsync(request, token)).Should().Be(destination);
        (await Saved(destination, token)).Should().Be(body);
        var denied = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request with { IdempotencyKey = Guid.NewGuid() }, token));
        denied.StatusCode.Should().Be(StatusCode.NotFound);
        (await fixture.ReadAsync(db => db.ExperienceEntries.IgnoreQueryFilters().CountAsync(row => row.AccountId == account.Id))).Should().Be(3);
        (await fixture.ReadAsync(db => db.ExperienceUnknownReferences.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(5);
    }

    [Fact]
    public async Task ChangedIntentAndCreateCopyCollisionConflictWithoutWritesAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var source = await fixture.MutateAsync(Create(Draft()), token);
        var request = Copy(source);
        var destination = await fixture.MutateAsync(request, token);
        foreach (var changed in new[]
        {
            request with { Copy = request.Copy! with { SourceEntryId = Guid.NewGuid() } },
            request with { Copy = request.Copy! with { SourceRevision = 2 } },
            request with { Copy = request.Copy! with { Occurrence = Occurrence(13) } },
            request with { Copy = request.Copy! with { TargetChoice = JournalCopyTargetChoice.UnknownFallback, UnknownLabel = "fallback" } },
            Create(Draft()) with { IdempotencyKey = request.IdempotencyKey },
        })
        {
            var error = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(changed, token));
            error.StatusCode.Should().Be(StatusCode.Aborted);
        }

        var create = Create(Draft());
        await fixture.MutateAsync(create, token);
        (await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request with { IdempotencyKey = create.IdempotencyKey }, token)))
            .StatusCode.Should().Be(StatusCode.Aborted);
        var fallback = request with { IdempotencyKey = Guid.NewGuid(), Copy = request.Copy! with { TargetChoice = JournalCopyTargetChoice.UnknownFallback, UnknownLabel = "private", ProducerLabel = "label" } };
        await fixture.MutateAsync(fallback, token);
        foreach (var copy in new[] { fallback.Copy! with { UnknownLabel = "changed" }, fallback.Copy! with { ProducerLabel = "changed" } })
        {
            (await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(fallback with { Copy = copy }, token))).StatusCode.Should().Be(StatusCode.Aborted);
        }

        // The trusted owner-internal origin is part of Copy intent identity too.
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var store = new ExperienceJournalStore(db, new CustomerJournalAccounts(db), TimeProvider.System);
            var trusted = request with { Identity = new CustomerAccountIdentity(account.IdentityIssuer, account.IdentitySubject), OriginStorefrontId = Guid.NewGuid() };
            await Assert.ThrowsAsync<JournalMutationConflictException>(() => store.MutateAsync(trusted));
            await transaction.RollbackAsync();
            return true;
        });
        (await fixture.MutateAsync(request, token)).Should().Be(destination);
        (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(4);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(4);
    }

    [Fact]
    public async Task CreateHashRemainsByteCompatibleWithPreCopyReceiptAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var request = Create(Draft());
        var saved = await fixture.MutateAsync(request, fixture.Token(account.IdentitySubject));
        var header = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId));
        var receipt = await fixture.ReadAsync(db => db.ExperienceMutationReceipts.SingleAsync(row => row.IdempotencyKey == request.IdempotencyKey));
        var legacyHash = ExperienceCanonicalJson.Hash(ExperienceCanonicalJson.Serialize(new
        {
            request.Operation,
            OriginStorefrontId = (Guid?)header.OriginStorefrontId,
            request.EntryId,
            request.ExpectedRevision,
            request.Draft,
        }));
        receipt.RequestHash.Should().Be(legacyHash);
        (await fixture.MutateAsync(request, fixture.Token(account.IdentitySubject))).Should().Be(saved);
        receipt.ExpiresAtUtc.Should().Be(receipt.CreatedAtUtc.AddHours(24));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedCreateOrCopyBodyIgnoresLaterTargetDateScoreEditsAndDeniesDeletedDestinationAsync(bool copy)
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var source = await fixture.MutateAsync(Create(Draft()), token);
        var request = copy ? Copy(source) : Create(Draft());
        var result = await fixture.MutateAsync(request, token);
        var original = await Saved(result, token);
        var mapping = Edit(result, Draft(Guid.NewGuid()) with { Occurrence = Occurrence(20), Content = ExperienceContent.Create("different body", 5, [], []) })
            with
        { Operation = ExperienceMutationOperation.MapTarget };
        var edited = await fixture.MutateAsync(mapping, token);
        (await fixture.MutateAsync(request, token)).Should().Be(result);
        (await Saved(result, token)).Should().Be(original);
        (await Saved(edited, token)).CanonicalPayload.Should().NotBe(original.CanonicalPayload);
        (await fixture.QueryAsync(new GetJournalEntryQuery(Identity, result.EntryId), token)).Revision.Should().Be(2);
        await fixture.MutateAsync(Delete(edited), token);
        // Internal result replay retains the receipt, while ordinary saved-body serving denies deletion.
        (await fixture.MutateAsync(request, token)).Should().Be(result);
        (await Assert.ThrowsAsync<RpcException>(() => Saved(result, token))).StatusCode.Should().Be(StatusCode.NotFound);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.SingleAsync(row => row.IdempotencyKey == request.IdempotencyKey)))
            .ResultRevision.Should().Be(result.Revision);
    }

    [Fact]
    public async Task ForeignMissingRevisionLifecycleAndServicePurposeAreDeniedAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var other = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var foreignToken = fixture.Token(other.IdentitySubject);
        var source = await fixture.MutateAsync(Create(Draft()), token);
        var request = Copy(source);
        foreach (var attempt in new[] { request, request with { Copy = request.Copy! with { SourceEntryId = Guid.NewGuid() } } })
        {
            (await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(attempt, foreignToken))).StatusCode.Should().Be(StatusCode.NotFound);
        }

        (await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request with { Copy = request.Copy! with { SourceRevision = 99 } }, token)))
            .StatusCode.Should().Be(StatusCode.NotFound);
        (await Assert.ThrowsAsync<RpcException>(() => Saved(source, foreignToken))).StatusCode.Should().Be(StatusCode.NotFound);
        (await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new GetJournalEntryQuery(Identity, source.EntryId, 99), token))).StatusCode.Should().Be(StatusCode.NotFound);
        (await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new GetJournalEntryQuery(Identity, source.EntryId, 0), token))).StatusCode.Should().Be(StatusCode.InvalidArgument);
        (await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request, fixture.Token(account.IdentitySubject, "\"service:v1\"")))).StatusCode.Should().Be(StatusCode.PermissionDenied);
        var destination = await fixture.MutateAsync(request, token);
        await fixture.ChangeAsync(db => db.Entry(db.CustomerAccounts.Single(row => row.Id == account.Id)).Property(row => row.Status).CurrentValue = CustomerAccountStatusType.Blocked);
        (await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request, token))).StatusCode.Should().Be(StatusCode.PermissionDenied);
        (await Assert.ThrowsAsync<RpcException>(() => Saved(destination, token))).StatusCode.Should().Be(StatusCode.PermissionDenied);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == other.Id))).Should().Be(0);
    }

    [Fact]
    public async Task ProductRetainingCopyFailsClosedAndExplicitFallbackCopiesOnlyPrivateUnknownAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var source = await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), token);
        var request = Copy(source);
        (await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request, token))).StatusCode.Should().Be(StatusCode.FailedPrecondition);
        (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        (await fixture.ReadAsync(db => db.ExperienceUnknownReferences.CountAsync(row => row.AccountId == account.Id))).Should().Be(0);
        var fallback = request with { Copy = request.Copy! with { TargetChoice = JournalCopyTargetChoice.UnknownFallback, UnknownLabel = "Owner-confirmed fallback", ProducerLabel = "private producer" } };
        var saved = await fixture.MutateAsync(fallback, token);
        var header = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId));
        header.ProductId.Should().BeNull();
        header.ReleaseId.Should().BeNull();
        header.RetainedTargetLabel.Should().Be("Owner-confirmed fallback");
        var unknown = await fixture.QueryAsync(new GetJournalUnknownQuery(Identity, header.UnknownReferenceId!.Value), token);
        unknown.ProducerLabel.Should().Be("private producer");
        (await fixture.MutateAsync(fallback, token)).Should().Be(saved);
    }

    [Fact]
    public async Task CopyPreservesVerifiedFrozenProfileExactObservationAndChosenTimeAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var catalog = Guid.NewGuid();
        var category = Guid.NewGuid();
        var definition = Guid.NewGuid();
        var profile = ExperienceProfileSnapshotEntity.CreateFrozen(catalog, category, "category", 1,
            [new ExperienceFrozenDefinition(definition, catalog, category, "Value", ProductExperienceObservationRole.Observation,
                ProductExperienceObservationValueType.Decimal, false, null, [], [])]);
        await fixture.ReadAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.ExperienceProfileSnapshots.Add(profile);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        });
        var draft = Draft() with
        {
            ProfileSnapshotId = profile.Id,
            CatalogId = catalog,
            CategoryId = category,
            Content = ExperienceContent.Create("frozen", 1, [], [ExperienceObservationValue.Create(definition,
                ProductExperienceObservationRole.Observation, ProductExperienceObservationValueType.Decimal, decimalValue: ExperienceObservationValue.MaximumDecimal)])
        };
        var source = await fixture.MutateAsync(Create(draft), token);
        var time = ExperienceOccurrence.Create(new DateOnly(2026, 11, 1), ExperienceTimePrecision.LocalTime,
            new TimeOnly(1, 30).Add(TimeSpan.FromTicks(1)), "America/New_York", -300);
        var request = Copy(source);
        var saved = await fixture.MutateAsync(request with { Copy = request.Copy! with { Occurrence = time } }, token);
        var body = await Saved(saved, token);
        body.CanonicalSchema.Should().Be(profile.CanonicalSchema);
        var revision = await fixture.ReadAsync(db => db.ExperienceRevisions.Include(row => row.Observations).SingleAsync(row => row.EntryId == saved.EntryId));
        revision.Observations.Single().DecimalValue.Should().Be(ExperienceObservationValue.MaximumDecimal);
        var header = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId));
        header.LocalTimeIso.Should().Be("01:30:00.0000001");
        header.UtcOffsetMinutes.Should().Be(-300);
    }

    [Fact]
    public async Task CorruptSourceFailsWithoutWritesAndCompletedReplayDoesNotReadItAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var source = await fixture.MutateAsync(Create(Draft()), token);
        var request = Copy(source);
        var destination = await fixture.MutateAsync(request, token);
        await fixture.ReadAsync(async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE journal_experience_revisions SET canonical_payload = canonical_payload || ' ' WHERE account_id = {account.Id} AND entry_id = {source.EntryId}");
            return true;
        });
        (await fixture.MutateAsync(request, token)).Should().Be(destination);
        var failure = await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request with { IdempotencyKey = Guid.NewGuid() }, token));
        failure.StatusCode.Should().Be(StatusCode.FailedPrecondition);
        failure.Status.Detail.Should().NotContain("original private note");
        (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(2);
        (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.AccountId == account.Id))).Should().Be(2);
        (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(2);
    }

    [Fact]
    public async Task CopyReceiptExpiresAtExactlyTwentyFourHoursAndFailedFreshSourceReadPreservesItAsync()
    {
        var clock = new JournalFixtureClock(new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero).AddTicks(7));
        var timed = new JournalHostFixture { Clock = clock };
        try
        {
            await timed.InitializeAsync();
            var account = await timed.SeedAccountAsync();
            var token = timed.Token(account.IdentitySubject);
            var source = await timed.MutateAsync(Create(Draft()), token);
            var request = Copy(source);
            var first = await timed.MutateAsync(request, token);
            var receipt = await timed.ReadAsync(db => db.ExperienceMutationReceipts.SingleAsync(row => row.IdempotencyKey == request.IdempotencyKey));
            receipt.ExpiresAtUtc.Should().Be(receipt.CreatedAtUtc.AddHours(24));
            clock.Now = receipt.ExpiresAtUtc - TimeSpan.FromMicroseconds(1);
            (await timed.MutateAsync(request, token)).Should().Be(first);
            clock.Now = receipt.ExpiresAtUtc;
            var second = await timed.MutateAsync(request, token);
            second.EntryId.Should().NotBe(first.EntryId);
            var renewed = await timed.ReadAsync(db => db.ExperienceMutationReceipts.SingleAsync(row => row.IdempotencyKey == request.IdempotencyKey));
            await timed.MutateAsync(Delete(source), token);
            (await timed.MutateAsync(request, token)).Should().Be(second);
            clock.Now = renewed.ExpiresAtUtc;
            (await Assert.ThrowsAsync<RpcException>(() => timed.MutateAsync(request, token))).StatusCode.Should().Be(StatusCode.NotFound);
            (await timed.ReadAsync(db => db.ExperienceMutationReceipts.SingleAsync(row => row.IdempotencyKey == request.IdempotencyKey)))
                .EntryId.Should().Be(second.EntryId);
            (await timed.ReadAsync(db => db.ExperienceEntries.IgnoreQueryFilters().CountAsync(row => row.AccountId == account.Id))).Should().Be(3);
        }
        finally
        {
            await timed.DisposeAsync();
        }
    }

    [Fact]
    public async Task FailureAfterCopySaveRollsBackDestinationRevisionFallbackAndReceiptAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var source = await fixture.MutateAsync(Create(Draft()), token);
        var request = Copy(source);
        request = request with { Copy = request.Copy! with { TargetChoice = JournalCopyTargetChoice.UnknownFallback, UnknownLabel = "new private" } };
        fixture.Failure.Key = request.IdempotencyKey;
        try
        {
            (await Assert.ThrowsAsync<RpcException>(() => fixture.MutateAsync(request, token))).StatusCode.Should().Be(StatusCode.FailedPrecondition);
            fixture.Failure.SawSavedReceipt.Should().BeTrue();
            (await fixture.ReadAsync(db => db.ExperienceEntries.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
            (await fixture.ReadAsync(db => db.ExperienceRevisions.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
            (await fixture.ReadAsync(db => db.ExperienceUnknownReferences.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
            (await fixture.ReadAsync(db => db.ExperienceMutationReceipts.CountAsync(row => row.AccountId == account.Id))).Should().Be(1);
        }
        finally
        {
            fixture.Failure.Key = null;
        }

        (await fixture.MutateAsync(request, token)).Revision.Should().Be(1);
    }
}
