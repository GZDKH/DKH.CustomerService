using System.Text.Json;
using DKH.CustomerService.Application.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Infrastructure.Persistence.Repositories;

public sealed partial class ExperienceJournalStore
{
    public async Task<JournalEntryPage> ListAsync(ListJournalEntriesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidatePageSize(query.PageSize);
        if (query.ProductId == Guid.Empty || query.Cursor?.EntryId == Guid.Empty)
        {
            throw new ArgumentException("A supplied journal filter or cursor requires a nonempty identifier.");
        }

        var owner = await ResolveLockedOwnerAsync(query.Identity, false, cancellationToken);
        var entries = db.ExperienceEntries.AsNoTracking().Where(row => row.AccountId == owner);
        if (query.ProductId.HasValue)
        {
            entries = entries.Where(row => row.ProductId == query.ProductId);
        }

        if (query.Cursor is { } cursor)
        {
            entries = entries.Where(row => row.OccurredDate < cursor.OccurredDate
                || (row.OccurredDate == cursor.OccurredDate && row.Id.CompareTo(cursor.EntryId) < 0));
        }

        var rows = await entries.OrderByDescending(row => row.OccurredDate).ThenByDescending(row => row.Id)
            .Take(query.PageSize + 1).Select(row => new JournalEntrySummary(row.Id, row.OriginStorefrontId,
                row.ProductId, row.ReleaseId, row.UnknownReferenceId, row.RetainedTargetLabel,
                row.OccurredDate, row.SessionScore, row.CurrentRevision)).ToArrayAsync(cancellationToken);
        var items = rows.Take(query.PageSize).ToArray();
        var next = rows.Length > query.PageSize ? new JournalEntryCursor(items[^1].OccurredDate, items[^1].EntryId) : null;
        return new JournalEntryPage(Array.AsReadOnly(items), next);
    }

    public async Task<JournalRevisionView> GetAsync(GetJournalEntryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var owner = await ResolveLockedOwnerAsync(query.Identity, false, cancellationToken);
        var entry = await db.ExperienceEntries.AsNoTracking().SingleOrDefaultAsync(row => row.AccountId == owner
            && row.Id == query.EntryId, cancellationToken) ?? throw new JournalResourceNotFoundException();
        var revision = await db.ExperienceRevisions.AsNoTracking().SingleAsync(row => row.AccountId == owner
            && row.EntryId == entry.Id && row.Revision == entry.CurrentRevision, cancellationToken);
        return (await ReadVerifiedRevisionsAsync([revision], cancellationToken))[0];
    }

    public async Task<JournalHistoryPage> HistoryAsync(ListJournalHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidatePageSize(query.PageSize);
        if (query.BeforeRevision is < 1)
        {
            throw new ArgumentException("A history cursor requires a positive retained revision.");
        }

        var owner = await ResolveLockedOwnerAsync(query.Identity, false, cancellationToken);
        // Owner history includes the retained tombstone after an explicit delete.
        if (!await db.ExperienceEntries.IgnoreQueryFilters().AsNoTracking().AnyAsync(row => row.AccountId == owner
                && row.Id == query.EntryId, cancellationToken))
        {
            throw new JournalResourceNotFoundException();
        }

        var revisions = db.ExperienceRevisions.AsNoTracking().Where(row => row.AccountId == owner && row.EntryId == query.EntryId);
        if (query.BeforeRevision.HasValue)
        {
            revisions = revisions.Where(row => row.Revision < query.BeforeRevision.Value);
        }

        var rows = await revisions.OrderByDescending(row => row.Revision).Take(query.PageSize + 1).ToArrayAsync(cancellationToken);
        var selected = rows.Take(query.PageSize).ToArray();
        var items = await ReadVerifiedRevisionsAsync(selected, cancellationToken);
        return new JournalHistoryPage(Array.AsReadOnly(items), rows.Length > query.PageSize ? selected[^1].Revision : null);
    }

    public async Task<JournalUnknownView> UnknownAsync(GetJournalUnknownQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var owner = await ResolveLockedOwnerAsync(query.Identity, false, cancellationToken);
        return await db.ExperienceUnknownReferences.AsNoTracking().Where(row => row.AccountId == owner && row.Id == query.ReferenceId)
            .Select(row => new JournalUnknownView(row.Id, row.OwnerLabel, row.ProducerLabel)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new JournalResourceNotFoundException();
    }

    private async Task<JournalRevisionView[]> ReadVerifiedRevisionsAsync(ExperienceRevisionEntity[] revisions, CancellationToken cancellationToken)
    {
        var ids = revisions.Select(row => row.ProfileSnapshotId).Distinct().ToArray();
        var profiles = await db.ExperienceProfileSnapshots.AsNoTracking().Where(row => ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var result = new JournalRevisionView[revisions.Length];
        for (var index = 0; index < revisions.Length; index++)
        {
            var revision = revisions[index];
            if (!profiles.TryGetValue(revision.ProfileSnapshotId, out var profile))
            {
                throw new InvalidOperationException("The retained revision schema is unavailable.");
            }

            profile.ReadDefinitions();
            var payload = revision.ReadCanonicalPayload();
            using var body = JsonDocument.Parse(payload);
            if (!body.RootElement.TryGetProperty("schemaHash", out var schemaHash)
                || schemaHash.ValueKind != JsonValueKind.String || schemaHash.GetString() != profile.SchemaHash)
            {
                throw new InvalidOperationException("The retained revision schema binding failed its integrity check.");
            }

            result[index] = new JournalRevisionView(revision.EntryId, revision.Revision, revision.CreatedAtUtc, revision.IsDeletion,
                payload, revision.PayloadHash, profile.Id, profile.ReadCanonicalSchema(), profile.SchemaHash);
        }

        return result;
    }

    private static void ValidatePageSize(int size)
    {
        if (size is < 1 or > 50)
        {
            throw new ArgumentException("Journal reads require a page size from 1 through 50.");
        }
    }
}
