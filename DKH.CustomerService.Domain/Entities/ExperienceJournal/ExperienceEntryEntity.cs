using DKH.CustomerService.Domain.ValueObjects;
using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ExperienceJournal;

public sealed class ExperienceEntryEntity : FullAuditedEntityWithKey<Guid>
{
    private readonly List<ExperienceRevisionEntity> _revisions = [];

    private ExperienceEntryEntity()
    {
        RetainedTargetLabel = string.Empty;
    }

    private ExperienceEntryEntity(Guid accountId, Guid originStorefrontId, DateTime createdAtUtc)
        : base(Guid.NewGuid())
    {
        AccountId = accountId;
        OriginStorefrontId = originStorefrontId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        RetainedTargetLabel = string.Empty;
    }

    public Guid AccountId { get; private set; }
    public Guid OriginStorefrontId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? UnknownReferenceId { get; private set; }
    public Guid? ReleaseId { get; private set; }
    public string RetainedTargetLabel { get; private set; }
    public DateOnly OccurredDate { get; private set; }
    public ExperienceTimePrecision TimePrecision { get; private set; }
    public string? LocalTimeIso { get; private set; }
    public string? TimeZone { get; private set; }
    public int? UtcOffsetMinutes { get; private set; }
    public int? SessionScore { get; private set; }
    public long CurrentRevision { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<ExperienceRevisionEntity> Revisions => _revisions.AsReadOnly();

    public override object?[] GetKeys() => [Id];

    public static ExperienceEntryEntity Create(Guid accountId, Guid originStorefrontId, ExperienceTarget target,
        ExperienceOccurrence occurrence, ExperienceContent content, ExperienceProfileSnapshotEntity profile, DateTime createdAtUtc,
        Guid? catalogId = null, Guid? categoryId = null, ExperienceUnknownReferenceEntity? unknown = null)
    {
        if (accountId == Guid.Empty || originStorefrontId == Guid.Empty || createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Canonical owner, trusted origin storefront and UTC clock are required.");
        }

        ValidateUnknown(accountId, target, unknown);
        var entity = new ExperienceEntryEntity(accountId, originStorefrontId, createdAtUtc);
        var revision = ExperienceRevisionEntity.Create(accountId, entity.Id, 1, profile, target, occurrence, content, createdAtUtc, catalogId, categoryId);
        entity.Apply(revision, target, occurrence, content.SessionScore);
        return entity;
    }

    public ExperienceRevisionEntity Append(long expectedRevision, ExperienceTarget target, ExperienceOccurrence occurrence,
        ExperienceContent content, ExperienceProfileSnapshotEntity profile, DateTime createdAtUtc,
        Guid? catalogId = null, Guid? categoryId = null, ExperienceUnknownReferenceEntity? unknown = null)
    {
        RequireCurrent(expectedRevision);
        ValidateUnknown(AccountId, target, unknown);
        var revision = ExperienceRevisionEntity.Create(AccountId, Id, CurrentRevision + 1, profile, target, occurrence, content, createdAtUtc, catalogId, categoryId);
        Apply(revision, target, occurrence, content.SessionScore);
        return revision;
    }

    public ExperienceRevisionEntity AppendDeletion(long expectedRevision, ExperienceRevisionEntity current, DateTime createdAtUtc)
    {
        RequireCurrent(expectedRevision);
        ArgumentNullException.ThrowIfNull(current);
        if (current.EntryId != Id || current.AccountId != AccountId || current.Revision != CurrentRevision)
        {
            throw new InvalidOperationException("An owned current revision is required for deletion.");
        }

        var revision = ExperienceRevisionEntity.CreateDeletion(current, createdAtUtc);
        CurrentRevision = revision.Revision;
        UpdatedAtUtc = revision.CreatedAtUtc;
        IsDeleted = true;
        _revisions.Add(revision);
        return revision;
    }

    private void RequireCurrent(long expectedRevision)
    {
        if (IsDeleted || CurrentRevision < 1 || expectedRevision != CurrentRevision || CurrentRevision == long.MaxValue)
        {
            throw new ExperienceRevisionConflictException();
        }
    }

    private void Apply(ExperienceRevisionEntity revision, ExperienceTarget target, ExperienceOccurrence occurrence, int? score)
    {
        ProductId = target.ProductId;
        UnknownReferenceId = target.UnknownReferenceId;
        ReleaseId = target.ReleaseId;
        RetainedTargetLabel = target.RetainedLabel;
        OccurredDate = occurrence.OccurredDate;
        TimePrecision = occurrence.Precision;
        LocalTimeIso = occurrence.LocalTimeIso;
        TimeZone = occurrence.TimeZone;
        UtcOffsetMinutes = occurrence.UtcOffsetMinutes;
        SessionScore = score;
        CurrentRevision = revision.Revision;
        UpdatedAtUtc = revision.CreatedAtUtc;
        _revisions.Add(revision);
    }

    private static void ValidateUnknown(Guid accountId, ExperienceTarget target, ExperienceUnknownReferenceEntity? unknown)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.UnknownReferenceId.HasValue && (unknown is null || unknown.Id != target.UnknownReferenceId
            || unknown.AccountId != accountId || unknown.IsDeleted || unknown.OwnerLabel != target.RetainedLabel))
        {
            throw new ArgumentException("An active owned private unknown reference is required.");
        }
    }
}

public sealed class ExperienceRevisionConflictException() : InvalidOperationException("The saved experience revision has changed.");
