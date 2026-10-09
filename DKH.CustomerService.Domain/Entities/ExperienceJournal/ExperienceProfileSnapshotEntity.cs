using System.Collections.ObjectModel;
using System.Text.Json;
using DKH.CustomerService.Domain.ValueObjects;
using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ExperienceJournal;

/// <summary>Immutable renderer schema retained independently of current catalog metadata.</summary>
public sealed class ExperienceProfileSnapshotEntity : FullAuditedEntityWithKey<Guid>
{
    private static readonly JsonSerializerOptions SchemaJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static readonly Guid GeneralV1Id = new("f9eb970a-7e55-54f3-9aaa-74e3383ee0de");

    public const string GeneralV1CanonicalSchema = /*lang=json,strict*/ """{"catalogId":null,"categoryId":null,"definitions":[],"rendererCode":"general","rendererVersion":1,"schemaVersion":1}""";

    public const string GeneralV1SchemaHash = "f9eb970a7e5524f39aaa74e3383ee0ded5e8f17450a9fc58956ccd0cfe512371";

    private ExperienceProfileSnapshotEntity()
    {
        RendererCode = string.Empty;
        CanonicalSchema = string.Empty;
        SchemaHash = string.Empty;
    }

    private ExperienceProfileSnapshotEntity(Guid id, string rendererCode, int rendererVersion,
        Guid? catalogId, Guid? categoryId, string canonicalSchema)
        : base(id)
    {
        RendererCode = rendererCode;
        RendererVersion = rendererVersion;
        CatalogId = catalogId;
        CategoryId = categoryId;
        SchemaVersion = 1;
        CanonicalSchema = canonicalSchema;
        SchemaHash = ExperienceCanonicalJson.Hash(canonicalSchema);
    }

    public string RendererCode { get; private set; }

    public int RendererVersion { get; private set; }

    public Guid? CatalogId { get; private set; }

    public Guid? CategoryId { get; private set; }

    public int SchemaVersion { get; private set; }

    public string CanonicalSchema { get; private set; }

    public string SchemaHash { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static ExperienceProfileSnapshotEntity CreateGeneralV1()
        => new(GeneralV1Id, "general", 1, null, null, GeneralV1CanonicalSchema);

    public static ExperienceProfileSnapshotEntity CreateFrozen(Guid catalogId, Guid categoryId,
        string rendererCode, int rendererVersion, IReadOnlyList<ExperienceFrozenDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (catalogId == Guid.Empty || categoryId == Guid.Empty || string.IsNullOrWhiteSpace(rendererCode)
            || rendererCode.Length > 64 || rendererCode != rendererCode.Trim() || rendererCode == "general" || rendererVersion < 1
            || definitions.Any(definition => definition is null || definition.CatalogId != catalogId || definition.CategoryId != categoryId)
            || definitions.Select(definition => (definition.DefinitionId, definition.Role)).Distinct().Count() != definitions.Count)
        {
            throw new ArgumentException("A resolved profile requires exact catalog/category binding, renderer and unique definitions.");
        }

        var schema = new FrozenSchema(catalogId, categoryId,
            [.. definitions.OrderBy(definition => definition.DisplayOrder).ThenBy(definition => definition.DefinitionId)
                .ThenBy(definition => definition.Role)], rendererCode, rendererVersion, 1);
        var canonical = ExperienceCanonicalJson.Serialize(schema);
        return new ExperienceProfileSnapshotEntity(Guid.NewGuid(), rendererCode, rendererVersion, catalogId, categoryId, canonical);
    }

    public string ReadCanonicalSchema()
    {
        if (!ExperienceCanonicalJson.MatchesHash(CanonicalSchema, SchemaHash))
        {
            throw new InvalidOperationException("The retained experience schema failed its integrity check.");
        }

        return CanonicalSchema;
    }

    public ReadOnlyCollection<ExperienceFrozenDefinition> ReadDefinitions()
    {
        try
        {
            var canonical = ReadCanonicalSchema();
            if (SchemaVersion != 1 || ExperienceCanonicalJson.Canonicalize(canonical) != canonical)
            {
                throw new InvalidOperationException("The retained experience schema version or serialization is unsupported.");
            }

            var schema = JsonSerializer.Deserialize<FrozenSchema>(canonical, SchemaJsonOptions);
            if (schema is null || schema.SchemaVersion != SchemaVersion || schema.CatalogId != CatalogId
                || schema.CategoryId != CategoryId || schema.RendererCode != RendererCode || schema.RendererVersion != RendererVersion
                || schema.Definitions is null
                || schema.Definitions.Any(definition => definition is null || definition.CatalogId != CatalogId || definition.CategoryId != CategoryId)
                || schema.Definitions.Select(definition => (definition.DefinitionId, definition.Role)).Distinct().Count() != schema.Definitions.Count)
            {
                throw new InvalidOperationException("The retained experience schema is inconsistent.");
            }

            if (RendererCode == "general")
            {
                if (Id != GeneralV1Id || ReadCanonicalSchema() != GeneralV1CanonicalSchema)
                {
                    throw new InvalidOperationException("The retained General schema is inconsistent.");
                }
            }
            else if (!CatalogId.HasValue || !CategoryId.HasValue || CatalogId == Guid.Empty || CategoryId == Guid.Empty)
            {
                throw new InvalidOperationException("The retained experience schema is missing its binding.");
            }

            return Array.AsReadOnly(schema.Definitions.ToArray());
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            // Do not attach parser exceptions that can reveal frozen labels or private content.
            throw new InvalidOperationException("The retained experience schema failed its integrity check.");
        }
    }

    public void ValidateObservations(IEnumerable<ExperienceObservationValue> observations, Guid? catalogId, Guid? categoryId)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (catalogId != CatalogId || categoryId != CategoryId)
        {
            throw new ArgumentException("Observation context does not match the frozen catalog/category binding.");
        }

        var definitions = ReadDefinitions().ToDictionary(definition => (definition.DefinitionId, definition.Role));
        foreach (var observation in observations)
        {
            if (observation is null || !definitions.TryGetValue((observation.DefinitionId, observation.Role), out var definition))
            {
                throw new ArgumentException("Observation definition is not declared by the retained profile.", nameof(observations));
            }

            definition.ValidateObservation(observation);
        }
    }

    public void ValidateGeneralObservations(IEnumerable<ExperienceObservationValue> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (Id != GeneralV1Id || RendererCode != "general" || RendererVersion != 1
            || CatalogId.HasValue || CategoryId.HasValue || SchemaVersion != 1
            || ReadCanonicalSchema() != GeneralV1CanonicalSchema || observations.Any())
        {
            throw new ArgumentException("General v1 has no observation definitions.", nameof(observations));
        }
    }

    private sealed record FrozenSchema(Guid? CatalogId, Guid? CategoryId, IReadOnlyList<ExperienceFrozenDefinition> Definitions,
        string RendererCode, int RendererVersion, int SchemaVersion);
}
