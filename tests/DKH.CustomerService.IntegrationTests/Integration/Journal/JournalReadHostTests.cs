using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Application.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.Enums;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.IntegrationTests.Integration.Journal;

[Trait("Category", "Integration")]
public sealed class JournalReadHostTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    private static readonly CustomerAccountIdentity UntrustedIdentity = new("ignored", "browser-cannot-select-owner");

    [Fact]
    public async Task KeysetReadsKeepSameDateEntriesAndProductFilterWithinOwnerAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var other = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var product = Guid.NewGuid();
        var saved = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            saved.Add((await fixture.MutateAsync(Create(Draft(product)), token)).EntryId);
        }

        await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), token);
        await fixture.MutateAsync(Create(Draft(product)), fixture.Token(other.IdentitySubject));
        var ids = new List<Guid>();
        JournalEntryCursor? cursor = null;
        do
        {
            var page = await fixture.QueryAsync(new ListJournalEntriesQuery(UntrustedIdentity, 2, cursor, product), token);
            page.Items.Should().HaveCountLessThanOrEqualTo(2);
            ids.AddRange(page.Items.Select(row => row.EntryId));
            cursor = page.NextCursor;
        } while (cursor is not null);

        ids.Should().BeEquivalentTo(saved);
        ids.Distinct().Should().HaveCount(5);
    }

    [Fact]
    public async Task CurrentAndHistoryReturnFrozenPayloadsUntilDeletionDeniesServingAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var draft = Draft(Guid.NewGuid());
        var saved = await fixture.MutateAsync(Create(draft), token);
        var original = await fixture.QueryAsync(new GetJournalEntryQuery(UntrustedIdentity, saved.EntryId), token);
        original.SchemaHash.Should().Be(ExperienceProfileSnapshotEntity.GeneralV1SchemaHash);
        original.CanonicalSchema.Should().Be(ExperienceProfileSnapshotEntity.GeneralV1CanonicalSchema);
        var edit = new MutateJournalCommand(UntrustedIdentity, Guid.NewGuid(), ExperienceMutationOperation.Update,
            Guid.NewGuid(), saved.EntryId, 1, draft with { Content = ExperienceContent.Create("second", 5, [], []) });
        await fixture.MutateAsync(edit, token);
        (await fixture.QueryAsync(new GetJournalEntryQuery(UntrustedIdentity, saved.EntryId), token)).Revision.Should().Be(2);
        var page = await fixture.QueryAsync(new ListJournalHistoryQuery(UntrustedIdentity, saved.EntryId, 1), token);
        page.Items.Select(row => row.Revision).Should().Equal(2);
        page.NextBeforeRevision.Should().Be(2);
        var oldest = await fixture.QueryAsync(new ListJournalHistoryQuery(UntrustedIdentity, saved.EntryId, 2, page.NextBeforeRevision), token);
        oldest.Items.Should().ContainSingle().Which.CanonicalPayload.Should().Be(original.CanonicalPayload);
        oldest.NextBeforeRevision.Should().BeNull();
        await fixture.MutateAsync(edit with
        {
            Operation = ExperienceMutationOperation.Delete,
            IdempotencyKey = Guid.NewGuid(),
            ExpectedRevision = 2,
            Draft = null
        }, token);
        var deleted = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new GetJournalEntryQuery(UntrustedIdentity, saved.EntryId), token));
        deleted.StatusCode.Should().Be(StatusCode.NotFound);
        var historyDenied = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new ListJournalHistoryQuery(UntrustedIdentity, saved.EntryId), token));
        historyDenied.Status.Should().Be(deleted.Status);
        // A trusted database assertion proves retained audit data. No normal
        // owner request, browser parameter or service token bypass exposes it.
        var retained = await fixture.ReadAsync(db => db.ExperienceRevisions.Where(row => row.EntryId == saved.EntryId)
            .OrderBy(row => row.Revision).ToArrayAsync());
        retained.Select(row => row.Revision).Should().Equal(1, 2, 3);
        retained[0].CanonicalPayload.Should().Be(original.CanonicalPayload);
        retained[2].IsDeletion.Should().BeTrue();
        (await fixture.QueryAsync(new ListJournalEntriesQuery(UntrustedIdentity), token)).Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task PageSizeOutsideContractBoundIsRejectedAsync(int size)
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var saved = await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), token);
        var list = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new ListJournalEntriesQuery(UntrustedIdentity, size), token));
        list.StatusCode.Should().Be(StatusCode.InvalidArgument);
        var history = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new ListJournalHistoryQuery(UntrustedIdentity, saved.EntryId, size), token));
        history.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task DefaultAndMaximumPageSizesMatchTheAcceptedContractAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var request = new ListJournalEntriesQuery(UntrustedIdentity);
        request.PageSize.Should().Be(25);
        (await fixture.QueryAsync(request with { PageSize = 100 }, token)).Items.Should().BeEmpty();
        var saved = await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), token);
        var history = new ListJournalHistoryQuery(UntrustedIdentity, saved.EntryId);
        history.PageSize.Should().Be(25);
        (await fixture.QueryAsync(history with { PageSize = 100 }, token)).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ForeignHistoryAndUnknownAreIndistinguishableFromMissingAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var other = await fixture.SeedAccountAsync();
        var saved = await fixture.MutateAsync(Create(Draft(null)), fixture.Token(account.IdentitySubject));
        var header = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId));
        var token = fixture.Token(other.IdentitySubject);
        var foreign = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new ListJournalHistoryQuery(UntrustedIdentity, saved.EntryId, 10), token));
        var missing = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new ListJournalHistoryQuery(UntrustedIdentity, Guid.NewGuid(), 10), token));
        foreign.StatusCode.Should().Be(StatusCode.NotFound);
        missing.Status.Should().Be(foreign.Status);
        var unknown = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new GetJournalUnknownQuery(UntrustedIdentity, header.UnknownReferenceId!.Value), token));
        unknown.StatusCode.Should().Be(StatusCode.NotFound);
        var owned = await fixture.QueryAsync(new GetJournalUnknownQuery(UntrustedIdentity, header.UnknownReferenceId!.Value), fixture.Token(account.IdentitySubject));
        owned.OwnerLabel.Should().Be("Private unknown");
    }

    [Fact]
    public async Task LifecycleAndServicePurposeGuardEveryReadAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var saved = await fixture.MutateAsync(Create(Draft(null)), token);
        var header = await fixture.ReadAsync(db => db.ExperienceEntries.SingleAsync(row => row.Id == saved.EntryId));
        var service = fixture.Token(account.IdentitySubject, "\"service:v1\"");
        var deniedService = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new ListJournalEntriesQuery(UntrustedIdentity, 10), service));
        deniedService.StatusCode.Should().Be(StatusCode.PermissionDenied);
        await fixture.ChangeAsync(db => db.Entry(db.CustomerAccounts.Single(row => row.Id == account.Id))
            .Property(row => row.Status).CurrentValue = CustomerAccountStatusType.DeletionPending);
        var denials = new[]
        {
            Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new ListJournalEntriesQuery(UntrustedIdentity, 10), token)),
            Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new GetJournalEntryQuery(UntrustedIdentity, saved.EntryId), token)),
            Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new ListJournalHistoryQuery(UntrustedIdentity, saved.EntryId, 10), token)),
            Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new GetJournalUnknownQuery(UntrustedIdentity, header.UnknownReferenceId!.Value), token)),
        };
        foreach (var error in await Task.WhenAll(denials))
        {
            error.StatusCode.Should().Be(StatusCode.PermissionDenied);
        }
    }

    [Fact]
    public async Task CorruptRetainedPayloadFailsClosedWithoutReturningPrivateBodyAsync()
    {
        var account = await fixture.SeedAccountAsync();
        var token = fixture.Token(account.IdentitySubject);
        var saved = await fixture.MutateAsync(Create(Draft(Guid.NewGuid())), token);
        await fixture.ReadAsync(async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE journal_experience_revisions SET canonical_payload = canonical_payload || ' ' WHERE account_id = {account.Id} AND entry_id = {saved.EntryId}");
            return true;
        });
        var error = await Assert.ThrowsAsync<RpcException>(() => fixture.QueryAsync(new GetJournalEntryQuery(UntrustedIdentity, saved.EntryId), token));
        error.StatusCode.Should().Be(StatusCode.FailedPrecondition);
        error.Status.Detail.Should().NotContain("first");
    }

    private static MutateJournalCommand Create(JournalDraft draft) => new(UntrustedIdentity, Guid.NewGuid(),
        ExperienceMutationOperation.Create, Guid.NewGuid(), null, null, draft);

    private static JournalDraft Draft(Guid? product) => new(new JournalTargetInput(product, null, null,
        product.HasValue ? null : "Private unknown", null, product.HasValue ? "Retained Product" : null),
        ExperienceOccurrence.Create(new DateOnly(2026, 10, 10), ExperienceTimePrecision.DateOnly),
        ExperienceContent.Create("first", null, [], []), ExperienceProfileSnapshotEntity.GeneralV1Id);
}
