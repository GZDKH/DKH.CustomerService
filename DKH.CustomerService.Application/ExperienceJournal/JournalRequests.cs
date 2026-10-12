using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.ValueObjects;

namespace DKH.CustomerService.Application.ExperienceJournal;

public sealed record JournalTargetInput(Guid? ProductId, Guid? ReleaseId, Guid? UnknownReferenceId,
    string? UnknownLabel, string? ProducerLabel, string? RetainedProductLabel);

public sealed record JournalDraft(JournalTargetInput Target, ExperienceOccurrence Occurrence, ExperienceContent Content,
    Guid ProfileSnapshotId, Guid? CatalogId = null, Guid? CategoryId = null);

public enum JournalCopyTargetChoice { Retain, UnknownFallback }

/// <summary>Source-bound intent; it is receipt input before any source body is read.</summary>
public sealed record JournalCopyIntent(Guid SourceEntryId, long SourceRevision, ExperienceOccurrence Occurrence,
    JournalCopyTargetChoice TargetChoice, string? UnknownLabel = null, string? ProducerLabel = null)
{
    public void Validate()
    {
        if (SourceEntryId == Guid.Empty || SourceRevision < 1 || Occurrence is null || !Enum.IsDefined(TargetChoice)
            || (TargetChoice == JournalCopyTargetChoice.Retain && (UnknownLabel is not null || ProducerLabel is not null))
            || (TargetChoice == JournalCopyTargetChoice.UnknownFallback
                && (string.IsNullOrWhiteSpace(UnknownLabel) || UnknownLabel.Length > 256 || ProducerLabel is { Length: > 256 })))
        {
            throw new ArgumentException("Copy requires an owned source revision, chosen occurrence and explicit bounded target choice.");
        }
    }
}

/// <summary>Owner-internal producer request. Identity and origin come from the trusted API adapter, never AccountId input.</summary>
public sealed record MutateJournalCommand(CustomerAccountIdentity Identity, Guid OriginStorefrontId,
    ExperienceMutationOperation Operation, Guid IdempotencyKey, Guid? EntryId, long? ExpectedRevision, JournalDraft? Draft,
    JournalCopyIntent? Copy = null)
    : IRequest<ExperienceMutationResult>;

public interface IExperienceJournalStore
{
    Task<ExperienceMutationResult> MutateAsync(MutateJournalCommand command, CancellationToken cancellationToken = default);
    Task<JournalEntryPage> ListAsync(ListJournalEntriesQuery query, CancellationToken cancellationToken = default);
    Task<JournalRevisionView> GetAsync(GetJournalEntryQuery query, CancellationToken cancellationToken = default);
    Task<JournalHistoryPage> HistoryAsync(ListJournalHistoryQuery query, CancellationToken cancellationToken = default);
    Task<JournalUnknownView> UnknownAsync(GetJournalUnknownQuery query, CancellationToken cancellationToken = default);
}

public sealed class MutateJournalCommandHandler(IExperienceJournalStore store)
    : IRequestHandler<MutateJournalCommand, ExperienceMutationResult>
{
    public Task<ExperienceMutationResult> Handle(MutateJournalCommand request, CancellationToken cancellationToken)
        => store.MutateAsync(request, cancellationToken);
}

public sealed class JournalMutationConflictException() : Exception("The saved experience or mutation key has changed.");
public sealed class JournalResourceNotFoundException() : Exception("Personal resource was not found.");
