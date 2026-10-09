using DKH.CustomerService.Application.CustomerAccounts;

namespace DKH.CustomerService.Application.ExperienceJournal;

public sealed record JournalEntryCursor(DateOnly OccurredDate, Guid EntryId);
public sealed record JournalEntrySummary(Guid EntryId, Guid OriginStorefrontId, Guid? ProductId, Guid? ReleaseId,
    Guid? UnknownReferenceId, string RetainedTargetLabel, DateOnly OccurredDate, int? SessionScore, long CurrentRevision);
public sealed record JournalEntryPage(IReadOnlyList<JournalEntrySummary> Items, JournalEntryCursor? NextCursor);
public sealed record JournalRevisionView(Guid EntryId, long Revision, DateTime CreatedAtUtc, bool IsDeleted,
    string CanonicalPayload, string PayloadHash, Guid ProfileSnapshotId, string CanonicalSchema, string SchemaHash);
public sealed record JournalHistoryPage(IReadOnlyList<JournalRevisionView> Items, long? NextBeforeRevision);
public sealed record JournalUnknownView(Guid ReferenceId, string OwnerLabel, string? ProducerLabel);

// Owner-internal reads retain the shared transaction boundary so the locked
// lifecycle decision remains valid until the bounded result has been read.
public sealed record ListJournalEntriesQuery(CustomerAccountIdentity Identity, int PageSize,
    JournalEntryCursor? Cursor = null, Guid? ProductId = null) : IRequest<JournalEntryPage>;
public sealed record GetJournalEntryQuery(CustomerAccountIdentity Identity, Guid EntryId) : IRequest<JournalRevisionView>;
public sealed record ListJournalHistoryQuery(CustomerAccountIdentity Identity, Guid EntryId, int PageSize,
    long? BeforeRevision = null) : IRequest<JournalHistoryPage>;
public sealed record GetJournalUnknownQuery(CustomerAccountIdentity Identity, Guid ReferenceId) : IRequest<JournalUnknownView>;

public sealed class ListJournalEntriesQueryHandler(IExperienceJournalStore store) : IRequestHandler<ListJournalEntriesQuery, JournalEntryPage>
{
    public Task<JournalEntryPage> Handle(ListJournalEntriesQuery request, CancellationToken cancellationToken)
        => store.ListAsync(request, cancellationToken);
}

public sealed class GetJournalEntryQueryHandler(IExperienceJournalStore store) : IRequestHandler<GetJournalEntryQuery, JournalRevisionView>
{
    public Task<JournalRevisionView> Handle(GetJournalEntryQuery request, CancellationToken cancellationToken)
        => store.GetAsync(request, cancellationToken);
}

public sealed class ListJournalHistoryQueryHandler(IExperienceJournalStore store) : IRequestHandler<ListJournalHistoryQuery, JournalHistoryPage>
{
    public Task<JournalHistoryPage> Handle(ListJournalHistoryQuery request, CancellationToken cancellationToken)
        => store.HistoryAsync(request, cancellationToken);
}

public sealed class GetJournalUnknownQueryHandler(IExperienceJournalStore store) : IRequestHandler<GetJournalUnknownQuery, JournalUnknownView>
{
    public Task<JournalUnknownView> Handle(GetJournalUnknownQuery request, CancellationToken cancellationToken)
        => store.UnknownAsync(request, cancellationToken);
}
