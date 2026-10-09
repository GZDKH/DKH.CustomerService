using System.Text.Json;
using System.Text.Json.Nodes;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ExperienceJournal;

public sealed class ExperienceRevisionEntity : FullAuditedEntityWithKey<Guid>
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly List<ExperienceRevisionObservationEntity> _observations = [];

    private ExperienceRevisionEntity()
    {
        CanonicalPayload = string.Empty;
        PayloadHash = string.Empty;
    }

    private ExperienceRevisionEntity(Guid accountId, Guid entryId, long revision, Guid profileSnapshotId,
        string canonicalPayload, DateTime createdAtUtc, bool isDeletion)
        : base(Guid.NewGuid())
    {
        AccountId = accountId;
        EntryId = entryId;
        Revision = revision;
        ProfileSnapshotId = profileSnapshotId;
        CanonicalPayload = canonicalPayload;
        PayloadHash = ExperienceCanonicalJson.Hash(canonicalPayload);
        SchemaVersion = 1;
        CreatedAtUtc = createdAtUtc;
        IsDeletion = isDeletion;
    }

    public Guid AccountId { get; private set; }
    public Guid EntryId { get; private set; }
    public long Revision { get; private set; }
    public Guid ProfileSnapshotId { get; private set; }
    public string CanonicalPayload { get; private set; }
    public string PayloadHash { get; private set; }
    public int SchemaVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsDeletion { get; private set; }
    public IReadOnlyCollection<ExperienceRevisionObservationEntity> Observations => _observations.AsReadOnly();

    public override object?[] GetKeys() => [Id];

    public static ExperienceRevisionEntity Create(Guid accountId, Guid entryId, long revision,
        ExperienceProfileSnapshotEntity profile, ExperienceTarget target, ExperienceOccurrence occurrence,
        ExperienceContent content, DateTime createdAtUtc, Guid? catalogId, Guid? categoryId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(content);
        ValidateIdentity(accountId, entryId, revision, createdAtUtc);
        profile.ValidateObservations(content.Observations, catalogId, categoryId);
        var payload = content.CanonicalPayload(target, occurrence, profile.Id, profile.SchemaHash);
        var entity = new ExperienceRevisionEntity(accountId, entryId, revision, profile.Id, payload, createdAtUtc, false);
        foreach (var value in content.Observations)
        {
            entity._observations.Add(ExperienceRevisionObservationEntity.Create(accountId, entity.Id, value));
        }

        return entity;
    }

    public static ExperienceRevisionEntity CreateDeletion(ExperienceRevisionEntity previous, DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (previous.IsDeletion || previous.Revision == long.MaxValue)
        {
            throw new InvalidOperationException("An active current revision is required for deletion.");
        }

        ValidateIdentity(previous.AccountId, previous.EntryId, previous.Revision + 1, createdAtUtc);
        var body = JsonNode.Parse(previous.ReadCanonicalPayload())!.AsObject();
        body["isDeleted"] = true;
        var payload = ExperienceCanonicalJson.Canonicalize(body.ToJsonString());
        var entity = new ExperienceRevisionEntity(previous.AccountId, previous.EntryId, previous.Revision + 1,
            previous.ProfileSnapshotId, payload, createdAtUtc, true);
        // Reconstruct from the verified full snapshot. EF navigation may be unloaded.
        var values = JsonSerializer.Deserialize<StoredObservation[]>(body["observations"]!.ToJsonString(), PayloadJsonOptions)
            ?? throw new InvalidOperationException("The retained observation snapshot is unavailable.");
        if (values.Length > 200)
        {
            throw new InvalidOperationException("The retained observation snapshot exceeds its bound.");
        }

        foreach (var observation in values)
        {
            entity._observations.Add(ExperienceRevisionObservationEntity.Create(previous.AccountId, entity.Id, observation.ReadValue()));
        }

        return entity;
    }

    public string ReadCanonicalPayload()
    {
        try
        {
            if (SchemaVersion != 1 || !ExperienceCanonicalJson.MatchesHash(CanonicalPayload, PayloadHash)
                || ExperienceCanonicalJson.Canonicalize(CanonicalPayload) != CanonicalPayload)
            {
                throw new InvalidOperationException("The retained revision failed its integrity check.");
            }

            using var body = JsonDocument.Parse(CanonicalPayload);
            if (body.RootElement.GetProperty("schemaVersion").GetInt32() != SchemaVersion
                || body.RootElement.GetProperty("profileSnapshotId").GetGuid() != ProfileSnapshotId
                || body.RootElement.GetProperty("isDeleted").GetBoolean() != IsDeletion)
            {
                throw new InvalidOperationException("The retained revision metadata is inconsistent.");
            }

            return CanonicalPayload;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InvalidOperationException("The retained experience revision failed its integrity check.");
        }
    }

    private static void ValidateIdentity(Guid accountId, Guid entryId, long revision, DateTime createdAtUtc)
    {
        if (accountId == Guid.Empty || entryId == Guid.Empty || revision < 1 || createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A canonical owner, entry, positive revision and UTC clock are required.");
        }
    }

    private sealed record StoredObservation(Guid DefinitionId, ProductExperienceObservationRole Role,
        ProductExperienceObservationValueType ValueType, string? TextValue, decimal? DecimalValue, long? IntegerValue,
        bool? BooleanValue, string? UnitCode, Guid? RowId, int? RowOrdinal, Guid? OptionId)
    {
        public ExperienceObservationValue ReadValue() => ExperienceObservationValue.Create(DefinitionId, Role, ValueType,
            TextValue, DecimalValue, IntegerValue, BooleanValue, UnitCode, RowId, RowOrdinal, OptionId);
    }
}
