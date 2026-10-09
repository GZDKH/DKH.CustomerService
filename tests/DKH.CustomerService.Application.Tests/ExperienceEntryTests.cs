using System.Reflection;
using System.Text.Json;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ExperienceEntryTests
{
    private static readonly DateTime Clock = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ThreeSessionsForTheSameProductAreIndependentDatedEntries()
    {
        var account = Guid.NewGuid();
        var origin = Guid.NewGuid();
        var target = ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product label");
        var entries = Enumerable.Range(1, 3).Select(day => ExperienceEntryEntity.Create(account, origin, target,
            Date(day), Note(null), General(), Clock)).ToArray();
        entries.Select(entry => entry.Id).Distinct().Should().HaveCount(3);
        entries.Select(entry => entry.Revisions.Single().Id).Distinct().Should().HaveCount(3);
        foreach (var entry in entries)
        {
            entry.AccountId.Should().Be(account);
            entry.OriginStorefrontId.Should().Be(origin);
            entry.ProductId.Should().Be(target.ProductId);
            entry.CurrentRevision.Should().Be(1);
            entry.SessionScore.Should().BeNull();
            entry.LocalTimeIso.Should().BeNull();
            entry.TimeZone.Should().BeNull();
            entry.UtcOffsetMinutes.Should().BeNull();
            entry.CreatedAtUtc.Should().Be(Clock);
            entry.OccurredDate.Should().NotBe(DateOnly.FromDateTime(Clock));
        }
    }

    [Fact]
    public void EditAppendsImmutablePayloadAndRetainsPriorLabelsAndContent()
    {
        var target = ExperienceTarget.Create(Guid.NewGuid(), null, null, "Retained label");
        var entry = ExperienceEntryEntity.Create(Guid.NewGuid(), Guid.NewGuid(), target, Date(1), Note("first", 5), General(), Clock);
        var first = entry.Revisions.Single();
        var before = first.ReadCanonicalPayload();
        var hash = first.PayloadHash;
        var edited = entry.Append(1, target, Date(2), Note("second"), General(), Clock.AddMinutes(1));
        entry.CurrentRevision.Should().Be(2);
        entry.SessionScore.Should().BeNull();
        entry.OccurredDate.Should().Be(new DateOnly(2026, 10, 2));
        entry.CreatedAtUtc.Should().Be(Clock);
        entry.UpdatedAtUtc.Should().Be(Clock.AddMinutes(1));
        first.ReadCanonicalPayload().Should().Be(before);
        first.PayloadHash.Should().Be(hash);
        edited.Revision.Should().Be(2);
        edited.Id.Should().NotBe(first.Id);
        using var original = JsonDocument.Parse(before);
        original.RootElement.GetProperty("privateNote").GetString().Should().Be("first");
        original.RootElement.GetProperty("target").GetProperty("retainedLabel").GetString().Should().Be("Retained label");
    }

    [Fact]
    public void StaleRevisionLeavesSavedEntryAndAuthorsDraftUnchanged()
    {
        var target = ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product");
        var entry = ExperienceEntryEntity.Create(Guid.NewGuid(), Guid.NewGuid(), target, Date(1), Note("first"), General(), Clock);
        entry.Append(1, target, Date(2), Note("winner"), General(), Clock.AddMinutes(1));
        var draft = Note("losing draft", 4);
        var before = entry.Revisions.Last().ReadCanonicalPayload();
        var act = () => entry.Append(1, target, Date(3), draft, General(), Clock.AddMinutes(2));
        act.Should().Throw<ExperienceRevisionConflictException>();
        entry.CurrentRevision.Should().Be(2);
        entry.Revisions.Should().HaveCount(2);
        entry.Revisions.Last().ReadCanonicalPayload().Should().Be(before);
        draft.PrivateNote.Should().Be("losing draft");
        draft.SessionScore.Should().Be(4);
    }

    [Fact]
    public void InvalidFrozenSemanticsFailBeforeHeaderChangesOrRevisionAppend()
    {
        var target = ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product");
        var entry = ExperienceEntryEntity.Create(Guid.NewGuid(), Guid.NewGuid(), target, Date(1), Note("first", 3), General(), Clock);
        var invalid = ExperienceContent.Create("changed", 5, [], [ExperienceObservationValue.Create(Guid.NewGuid(),
            ProductExperienceObservationRole.Observation, ProductExperienceObservationValueType.Boolean, booleanValue: false)]);
        var act = () => entry.Append(1, target, Date(2), invalid, General(), Clock.AddMinutes(1));
        act.Should().Throw<ArgumentException>();
        entry.CurrentRevision.Should().Be(1);
        entry.SessionScore.Should().Be(3);
        entry.OccurredDate.Should().Be(new DateOnly(2026, 10, 1));
        entry.UpdatedAtUtc.Should().Be(Clock);
        entry.Revisions.Should().ContainSingle();
    }

    [Fact]
    public void UnknownMappingRetainsOldUnknownIdentityAndLabelInHistory()
    {
        var account = Guid.NewGuid();
        var unknown = ExperienceUnknownReferenceEntity.Create(account, "Private unknown", "Private producer");
        var target = ExperienceTarget.Create(null, unknown.Id, null, unknown.OwnerLabel);
        var entry = ExperienceEntryEntity.Create(account, Guid.NewGuid(), target, Date(1), Note("note"), General(), Clock, unknown: unknown);
        var oldPayload = entry.Revisions.Single().ReadCanonicalPayload();
        var mapped = ExperienceTarget.Create(Guid.NewGuid(), null, Guid.NewGuid(), "Resolved Product");
        entry.Append(1, mapped, Date(1), Note("note"), General(), Clock.AddMinutes(1));
        entry.ProductId.Should().Be(mapped.ProductId);
        entry.ReleaseId.Should().Be(mapped.ReleaseId);
        entry.UnknownReferenceId.Should().BeNull();
        entry.Revisions.First().ReadCanonicalPayload().Should().Be(oldPayload);
        unknown.OwnerLabel.Should().Be("Private unknown");
        using var history = JsonDocument.Parse(oldPayload);
        history.RootElement.GetProperty("target").GetProperty("unknownReferenceId").GetGuid().Should().Be(unknown.Id);
        history.RootElement.GetProperty("target").GetProperty("retainedLabel").GetString().Should().Be("Private unknown");
    }

    [Fact]
    public void MissingForeignDeletedOrRelabelledUnknownReferenceIsRejected()
    {
        var account = Guid.NewGuid();
        var foreign = ExperienceUnknownReferenceEntity.Create(Guid.NewGuid(), "unknown");
        var owned = ExperienceUnknownReferenceEntity.Create(account, "owned");
        var deleted = ExperienceUnknownReferenceEntity.Create(account, "deleted");
        typeof(ExperienceUnknownReferenceEntity).GetProperty(nameof(deleted.IsDeleted))!.SetValue(deleted, true);
        foreach (var (target, reference) in new (ExperienceTarget, ExperienceUnknownReferenceEntity?)[]
                 { (ExperienceTarget.Create(null, foreign.Id, null, foreign.OwnerLabel), foreign),
                     (ExperienceTarget.Create(null, owned.Id, null, owned.OwnerLabel), null),
                     (ExperienceTarget.Create(null, deleted.Id, null, deleted.OwnerLabel), deleted),
                     (ExperienceTarget.Create(null, owned.Id, null, "changed label"), owned) })
        {
            var act = () => ExperienceEntryEntity.Create(account, Guid.NewGuid(), target, Date(1), Note(null), General(), Clock, unknown: reference);
            act.Should().Throw<ArgumentException>();
        }
    }

    [Theory]
    [InlineData(-240)]
    [InlineData(-300)]
    public void SelectedAmbiguousLocalTimeAndTickPrecisionAreRetained(int offset)
    {
        var time = new TimeOnly(1, 30).Add(TimeSpan.FromTicks(1));
        var occurrence = ExperienceOccurrence.Create(new DateOnly(2025, 11, 2), ExperienceTimePrecision.LocalTime,
            time, "America/New_York", offset);
        var entry = ExperienceEntryEntity.Create(Guid.NewGuid(), Guid.NewGuid(), ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product"),
            occurrence, Note(null), General(), Clock);
        entry.LocalTimeIso.Should().Be("01:30:00.0000001");
        entry.UtcOffsetMinutes.Should().Be(offset);
        entry.TimeZone.Should().Be("America/New_York");
        using var document = JsonDocument.Parse(entry.Revisions.Single().ReadCanonicalPayload());
        document.RootElement.GetProperty("occurrence").GetProperty("localTime").GetString().Should().Be(entry.LocalTimeIso);
    }

    [Fact]
    public void DeletionAppendsATombstoneAndPreservesThePreviousFullPayload()
    {
        var target = ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product");
        var entry = ExperienceEntryEntity.Create(Guid.NewGuid(), Guid.NewGuid(), target, Date(1), Note("private note", 5), General(), Clock);
        var original = entry.Revisions.Single();
        var before = original.ReadCanonicalPayload();
        var deletion = entry.AppendDeletion(1, original, Clock.AddMinutes(1));
        entry.IsDeleted.Should().BeTrue();
        entry.CurrentRevision.Should().Be(2);
        entry.Revisions.Should().HaveCount(2);
        original.ReadCanonicalPayload().Should().Be(before);
        deletion.IsDeletion.Should().BeTrue();
        deletion.ProfileSnapshotId.Should().Be(original.ProfileSnapshotId);
        using var deleted = JsonDocument.Parse(deletion.ReadCanonicalPayload());
        deleted.RootElement.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
        deleted.RootElement.GetProperty("privateNote").GetString().Should().Be("private note");
        var edit = () => entry.Append(2, target, Date(2), Note("restore"), General(), Clock.AddMinutes(2));
        edit.Should().Throw<ExperienceRevisionConflictException>();
    }

    [Fact]
    public void HeaderCannotDeleteUsingAnotherEntriesRevision()
    {
        var target = ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product");
        var account = Guid.NewGuid();
        var first = ExperienceEntryEntity.Create(account, Guid.NewGuid(), target, Date(1), Note(null), General(), Clock);
        var other = ExperienceEntryEntity.Create(account, Guid.NewGuid(), target, Date(1), Note(null), General(), Clock);
        var act = () => first.AppendDeletion(1, other.Revisions.Single(), Clock.AddMinutes(1));
        act.Should().Throw<InvalidOperationException>();
        first.IsDeleted.Should().BeFalse();
        first.CurrentRevision.Should().Be(1);
    }

    [Fact]
    public void TamperedHistoricalPayloadIsNeverReturned()
    {
        var entry = ExperienceEntryEntity.Create(Guid.NewGuid(), Guid.NewGuid(), ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product"),
            Date(1), Note("private note"), General(), Clock);
        var revision = entry.Revisions.Single();
        typeof(ExperienceRevisionEntity).GetProperty(nameof(revision.CanonicalPayload))!.SetValue(revision, "{}");
        var act = () => revision.ReadCanonicalPayload();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeletionReconstructsZeroFalseOptionAndRowValuesWhenNavigationIsUnloaded()
    {
        var catalog = Guid.NewGuid();
        var category = Guid.NewGuid();
        var numberId = Guid.NewGuid();
        var flagId = Guid.NewGuid();
        var option = new ExperienceFrozenOption(Guid.NewGuid(), "zero", ProductExperienceObservationValueType.Decimal,
            null, 0m, null, null);
        var profile = ExperienceProfileSnapshotEntity.CreateFrozen(catalog, category, "category", 1,
            [new ExperienceFrozenDefinition(numberId, catalog, category, "Number", ProductExperienceObservationRole.Observation,
                ProductExperienceObservationValueType.Decimal, false, null, [], [option], false),
                new ExperienceFrozenDefinition(flagId, catalog, category, "Flag", ProductExperienceObservationRole.Observation,
                    ProductExperienceObservationValueType.Boolean, false, null, [], [])]);
        var row = Guid.NewGuid();
        var content = ExperienceContent.Create("note", null, [],
            [ExperienceObservationValue.Create(numberId, ProductExperienceObservationRole.Observation,
                ProductExperienceObservationValueType.Decimal, decimalValue: 0m, rowId: row, rowOrdinal: 2, optionId: option.Id),
                ExperienceObservationValue.Create(flagId, ProductExperienceObservationRole.Observation,
                    ProductExperienceObservationValueType.Boolean, booleanValue: false)]);
        var entry = ExperienceEntryEntity.Create(Guid.NewGuid(), Guid.NewGuid(), ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product"),
            Date(1), content, profile, Clock, catalog, category);
        var previous = entry.Revisions.Single();
        previous.Observations.Should().HaveCount(2);
        // An EF query without Include has no loaded child navigation; the snapshot remains complete.
        ((List<ExperienceRevisionObservationEntity>)typeof(ExperienceRevisionEntity)
            .GetField("_observations", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(previous)!).Clear();
        var deletion = entry.AppendDeletion(1, previous, Clock.AddMinutes(1));
        deletion.Observations.Should().HaveCount(2);
        var numeric = deletion.Observations.Single(value => value.DefinitionId == numberId);
        numeric.AccountId.Should().Be(entry.AccountId);
        numeric.RevisionId.Should().Be(deletion.Id);
        numeric.DecimalValue.Should().Be(0m);
        numeric.OptionId.Should().Be(option.Id);
        numeric.RowId.Should().Be(row);
        numeric.RowOrdinal.Should().Be(2);
        deletion.Observations.Single(value => value.DefinitionId == flagId).BooleanValue.Should().BeFalse();
    }

    [Fact]
    public void EmptyOwnerOriginAndNonUtcClockAreRejected()
    {
        var target = ExperienceTarget.Create(Guid.NewGuid(), null, null, "Product");
        foreach (var (account, origin, clock) in new (Guid, Guid, DateTime)[]
                 { (Guid.Empty, Guid.NewGuid(), Clock), (Guid.NewGuid(), Guid.Empty, Clock),
                     (Guid.NewGuid(), Guid.NewGuid(), DateTime.SpecifyKind(Clock, DateTimeKind.Unspecified)) })
        {
            var act = () => ExperienceEntryEntity.Create(account, origin, target, Date(1), Note(null), General(), clock);
            act.Should().Throw<ArgumentException>();
        }

        var revision = () => ExperienceRevisionEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 0, General(), target,
            Date(1), Note(null), Clock, null, null);
        revision.Should().Throw<ArgumentException>();
    }

    private static ExperienceProfileSnapshotEntity General() => ExperienceProfileSnapshotEntity.CreateGeneralV1();
    private static ExperienceContent Note(string? note, int? score = null) => ExperienceContent.Create(note, score, [], []);
    private static ExperienceOccurrence Date(int day) => ExperienceOccurrence.Create(new DateOnly(2026, 10, day), ExperienceTimePrecision.DateOnly);
}
