using System.Text.Json;
using DKH.CustomerService.Application.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.Infrastructure.Persistence.Repositories;

public sealed partial class ExperienceJournalStore
{
    private static readonly JsonSerializerOptions CopyJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
        if (query.EntryId == Guid.Empty || query.Revision is < 1)
        {
            throw new ArgumentException("An entry and positive saved revision are required.");
        }

        var owner = await ResolveLockedOwnerAsync(query.Identity, false, cancellationToken);
        var entry = await db.ExperienceEntries.AsNoTracking().SingleOrDefaultAsync(row => row.AccountId == owner
            && row.Id == query.EntryId, cancellationToken) ?? throw new JournalResourceNotFoundException();
        var revision = await db.ExperienceRevisions.AsNoTracking().SingleOrDefaultAsync(row => row.AccountId == owner
            && row.EntryId == entry.Id && row.Revision == (query.Revision ?? entry.CurrentRevision), cancellationToken)
            ?? throw new JournalResourceNotFoundException();
        return (await ReadVerifiedRevisionsAsync([revision], cancellationToken))[0];
    }

    private async Task<JournalDraft> ResolveCopyDraftAsync(Guid owner, JournalCopyIntent copy, CancellationToken cancellationToken)
    {
        // The owner lifecycle is already locked for write. Check ordinary source
        // readability before selecting any retained private revision.
        if (!await db.ExperienceEntries.AsNoTracking().AnyAsync(row => row.AccountId == owner
                && row.Id == copy.SourceEntryId, cancellationToken))
        {
            throw new JournalResourceNotFoundException();
        }

        var revision = await db.ExperienceRevisions.AsNoTracking().SingleOrDefaultAsync(row => row.AccountId == owner
            && row.EntryId == copy.SourceEntryId && row.Revision == copy.SourceRevision, cancellationToken)
            ?? throw new JournalResourceNotFoundException();
        var saved = (await ReadVerifiedRevisionsAsync([revision], cancellationToken))[0];
        try
        {
            var body = JsonSerializer.Deserialize<CopyBody>(saved.CanonicalPayload, CopyJsonOptions)
                ?? throw new InvalidOperationException();
            if (saved.IsDeleted || body.Target is null || body.Tags is null || body.Observations is null)
            {
                throw new InvalidOperationException();
            }

            var sourceTarget = ExperienceTarget.Create(body.Target.ProductId, body.Target.UnknownReferenceId,
                body.Target.ReleaseId, body.Target.RetainedLabel);
            if (copy.TargetChoice == JournalCopyTargetChoice.Retain && sourceTarget.ProductId.HasValue)
            {
                // No authoritative Product adapter is available until EJ04.
                throw new InvalidOperationException();
            }

            var target = copy.TargetChoice == JournalCopyTargetChoice.UnknownFallback
                ? new JournalTargetInput(null, null, null, copy.UnknownLabel, copy.ProducerLabel, null)
                : new JournalTargetInput(null, null, sourceTarget.UnknownReferenceId, null, null, null);
            var content = ExperienceContent.Create(body.PrivateNote, body.SessionScore, body.Tags,
                body.Observations.Select(value => value.ReadValue()), body.PackLabel, body.LotLabel);
            // Only verified retained schema supplies profile context, never the
            // mutable source header or a fresh Catalog/profile lookup.
            using var schema = JsonDocument.Parse(saved.CanonicalSchema);
            var catalog = schema.RootElement.GetProperty("catalogId");
            var category = schema.RootElement.GetProperty("categoryId");
            return new JournalDraft(target, copy.Occurrence, content, saved.ProfileSnapshotId,
                catalog.ValueKind == JsonValueKind.Null ? null : catalog.GetGuid(),
                category.ValueKind == JsonValueKind.Null ? null : category.GetGuid());
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
            or KeyNotFoundException or FormatException or NullReferenceException)
        {
            throw new InvalidOperationException("The retained Copy source or required target adapter is unavailable.");
        }
    }

    private sealed record CopyBody(CopyTarget Target, string? PrivateNote, int? SessionScore, string? PackLabel,
        string? LotLabel, string[] Tags, CopyObservation[] Observations);
    private sealed record CopyTarget(Guid? ProductId, Guid? UnknownReferenceId, Guid? ReleaseId, string RetainedLabel);
    private sealed record CopyObservation(Guid DefinitionId, ProductExperienceObservationRole Role,
        ProductExperienceObservationValueType ValueType, string? TextValue, decimal? DecimalValue, long? IntegerValue,
        bool? BooleanValue, string? UnitCode, Guid? RowId, int? RowOrdinal, Guid? OptionId)
    {
        public ExperienceObservationValue ReadValue() => ExperienceObservationValue.Create(DefinitionId, Role, ValueType,
            TextValue, DecimalValue, IntegerValue, BooleanValue, UnitCode, RowId, RowOrdinal, OptionId);
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
        // Retention is not permission to serve a deleted entry's private body.
        // Purpose-limited purge remains separate from ordinary owner history.
        if (!await db.ExperienceEntries.AsNoTracking().AnyAsync(row => row.AccountId == owner
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
        if (size is < 1 or > 100)
        {
            throw new ArgumentException("Journal reads require a page size from 1 through 100.");
        }
    }
}
