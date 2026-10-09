using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ExperienceJournal;

public enum ExperienceMutationOperation
{
    Create = 1,
    Update = 2,
    Delete = 3,
    MapTarget = 4,
}

public sealed record ExperienceMutationResult(Guid EntryId, long Revision);

/// <summary>Immutable ordinary-write result; owner/lifecycle validation precedes receipt access.</summary>
public sealed class ExperienceMutationReceiptEntity : FullAuditedEntityWithKey<Guid>
{
    private ExperienceMutationReceiptEntity()
    {
        RequestHash = string.Empty;
    }

    private ExperienceMutationReceiptEntity(Guid accountId, ExperienceMutationOperation operation, Guid idempotencyKey,
        string requestHash, Guid entryId, long resultRevision, DateTime createdAtUtc)
        : base(Guid.NewGuid())
    {
        AccountId = accountId;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        EntryId = entryId;
        ResultRevision = resultRevision;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = createdAtUtc.AddHours(24);
    }

    public Guid AccountId { get; private set; }

    public ExperienceMutationOperation Operation { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public string RequestHash { get; private set; }

    public Guid EntryId { get; private set; }

    public long ResultRevision { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public ExperienceMutationResult OriginalResult => new(EntryId, ResultRevision);

    public override object?[] GetKeys() => [Id];

    public static ExperienceMutationReceiptEntity Create(Guid accountId, ExperienceMutationOperation operation,
        Guid idempotencyKey, string requestHash, Guid entryId, long resultRevision, DateTime createdAtUtc)
    {
        if (accountId == Guid.Empty || idempotencyKey == Guid.Empty || entryId == Guid.Empty
            || !Enum.IsDefined(operation) || resultRevision < 1 || createdAtUtc.Kind != DateTimeKind.Utc
            || createdAtUtc > DateTime.MaxValue.AddHours(-24)
            || requestHash is null || requestHash.Length != 64 || requestHash.Any(character => !char.IsAsciiHexDigitLower(character)))
        {
            throw new ArgumentException("An owner-scoped operation, key, canonical request hash, stable result and UTC clock are required.");
        }

        return new ExperienceMutationReceiptEntity(accountId, operation, idempotencyKey, requestHash, entryId, resultRevision, createdAtUtc);
    }

    public bool MatchesRequest(string requestHash) => StringComparer.Ordinal.Equals(RequestHash, requestHash);

    public bool IsReplayable(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Receipt expiry requires a UTC clock.", nameof(nowUtc));
        }

        return nowUtc >= CreatedAtUtc && nowUtc < ExpiresAtUtc;
    }
}
