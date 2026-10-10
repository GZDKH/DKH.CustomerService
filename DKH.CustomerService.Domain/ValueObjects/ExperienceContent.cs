namespace DKH.CustomerService.Domain.ValueObjects;

/// <summary>Bounded immutable author content copied before transactional mutation.</summary>
public sealed record ExperienceContent
{
    private ExperienceContent(string? privateNote, int? sessionScore, string? packLabel, string? lotLabel,
        string[] tags, ExperienceObservationValue[] observations)
    {
        PrivateNote = privateNote;
        SessionScore = sessionScore;
        PackLabel = packLabel;
        LotLabel = lotLabel;
        Tags = Array.AsReadOnly(tags);
        Observations = Array.AsReadOnly(observations);
    }

    public string? PrivateNote { get; }

    public int? SessionScore { get; }

    public string? PackLabel { get; }

    public string? LotLabel { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<ExperienceObservationValue> Observations { get; }

    public static ExperienceContent Create(string? privateNote, int? sessionScore, IEnumerable<string> tags,
        IEnumerable<ExperienceObservationValue> observations, string? packLabel = null, string? lotLabel = null)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(observations);
        if (privateNote is { Length: > 16000 } || sessionScore is < 1 or > 5
            || packLabel is { Length: > 120 } || lotLabel is { Length: > 120 })
        {
            throw new ArgumentException("Private note, score or contextual label exceeds its journal bound.");
        }

        var tagValues = tags.Take(51).Select(tag => tag?.Trim()).ToArray();
        if (tagValues.Length > 50 || tagValues.Any(tag => string.IsNullOrEmpty(tag) || tag.Length > 32)
            || tagValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != tagValues.Length)
        {
            throw new ArgumentException("Tags must be unique, nonempty, at most 50 and at most 32 characters each.", nameof(tags));
        }

        var values = observations.Take(201).ToArray();
        if (values.Length > 200 || values.Any(value => value is null)
            || values.Select(value => (value.DefinitionId, value.Role, value.RowId)).Distinct().Count() != values.Length)
        {
            throw new ArgumentException("Observations must be unique by definition, role and row, and bounded to 200.", nameof(observations));
        }

        var rows = values.Where(value => value.RowId.HasValue).GroupBy(value => value.RowId).ToArray();
        if (rows.Length > 12 || rows.Any(row => row.Select(value => value.RowOrdinal).Distinct().Count() != 1)
            || rows.Select(row => row.First().RowOrdinal).Distinct().Count() != rows.Length)
        {
            throw new ArgumentException("Repeated rows require at most 12 distinct identities and unique consistent ordinals.", nameof(observations));
        }

        return new ExperienceContent(privateNote, sessionScore, packLabel, lotLabel, [.. tagValues.Select(tag => tag!)], values);
    }

    public string CanonicalPayload(ExperienceTarget target, ExperienceOccurrence occurrence, Guid profileSnapshotId, string schemaHash,
        bool isDeleted = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(occurrence);
        if (profileSnapshotId == Guid.Empty || schemaHash is null || schemaHash.Length != 64
            || schemaHash.Any(character => !char.IsAsciiHexDigitLower(character)))
        {
            throw new ArgumentException("An immutable profile identifier and canonical SHA256 are required.");
        }

        return ExperienceCanonicalJson.Serialize(new
        {
            SchemaVersion = 1,
            IsDeleted = isDeleted,
            ProfileSnapshotId = profileSnapshotId,
            SchemaHash = schemaHash,
            Target = target,
            Occurrence = new
            {
                occurrence.OccurredDate,
                occurrence.Precision,
                LocalTime = occurrence.LocalTimeIso,
                occurrence.TimeZone,
                occurrence.UtcOffsetMinutes,
            },
            PrivateNote,
            SessionScore,
            PackLabel,
            LotLabel,
            Tags,
            Observations,
        });
    }
}
