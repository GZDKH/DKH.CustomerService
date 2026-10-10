using System.Globalization;
using System.Text.Json;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ExperienceContentTests
{
    [Fact]
    public void TargetRequiresExclusiveNonemptyIdentityAndProductForRelease()
    {
        var product = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        var release = Guid.NewGuid();
        ExperienceTarget.Create(product, null, release, "Product label").ReleaseId.Should().Be(release);
        ExperienceTarget.Create(null, unknown, null, "Private unknown label").UnknownReferenceId.Should().Be(unknown);
        foreach (var (p, u, r) in new (Guid?, Guid?, Guid?)[]
                 { (null, null, null), (product, unknown, null), (Guid.Empty, null, null),
                     (null, Guid.Empty, null), (product, null, Guid.Empty), (null, unknown, release) })
        {
            var act = () => ExperienceTarget.Create(p, u, r, "label");
            act.Should().Throw<ArgumentException>();
        }

        Action empty = () => ExperienceTarget.Create(product, null, null, " ");
        Action longLabel = () => ExperienceTarget.Create(product, null, null, new string('x', 257));
        empty.Should().Throw<ArgumentException>();
        longLabel.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(5)]
    public void OptionalScoreAndExactLimitsArePreserved(int? score)
    {
        var result = ExperienceContent.Create(new string('x', 16000), score,
            Enumerable.Range(0, 50).Select(index => index.ToString(CultureInfo.InvariantCulture)), [], new string('p', 120), new string('l', 120));
        result.PrivateNote!.Length.Should().Be(16000);
        result.SessionScore.Should().Be(score);
        result.Tags.Should().HaveCount(50);
        result.PackLabel!.Length.Should().Be(120);
        result.LotLabel!.Length.Should().Be(120);
    }

    [Fact]
    public void InvalidScoreNoteAndContextBoundsAreRejected()
    {
        static void note() => ExperienceContent.Create(new string('x', 16001), null, [], []);
        static void zero() => ExperienceContent.Create(null, 0, [], []);
        static void six() => ExperienceContent.Create(null, 6, [], []);
        static void pack() => ExperienceContent.Create(null, null, [], [], new string('x', 121));
        static void lot() => ExperienceContent.Create(null, null, [], [], lotLabel: new string('x', 121));
        foreach (var act in new[] { note, zero, six, pack, lot })
        {
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void TagsAreNormalizedUniqueAndBounded()
    {
        ExperienceContent.Create(null, null, [" tag ", new string('x', 32)], []).Tags[0].Should().Be("tag");
        foreach (var tags in new IEnumerable<string>[]
                 { ["tag", " TAG "], [" "], [new string('x', 33)], Enumerable.Range(0, 51).Select(index => index.ToString(CultureInfo.InvariantCulture)) })
        {
            var act = () => ExperienceContent.Create(null, null, tags, []);
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void ObservationUniquenessIncludesNullRowsAndRetainsDifferentRows()
    {
        var definition = Guid.NewGuid();
        var ordinary = Number(definition);
        var duplicate = () => ExperienceContent.Create(null, null, [], [ordinary, Number(definition)]);
        duplicate.Should().Throw<ArgumentException>();
        var rowId = Guid.NewGuid();
        var row = Number(definition, rowId, 1);
        var repeatedDuplicate = () => ExperienceContent.Create(null, null, [], [row, Number(definition, rowId, 1)]);
        repeatedDuplicate.Should().Throw<ArgumentException>();
        ExperienceContent.Create(null, null, [], [ordinary, row, Number(definition, Guid.NewGuid(), 2)])
            .Observations.Should().HaveCount(3);
    }

    [Fact]
    public void RowOrdinalsMustBeConsistentAndUniqueAcrossIdentities()
    {
        var rowId = Guid.NewGuid();
        var inconsistent = () => ExperienceContent.Create(null, null, [],
            [Number(Guid.NewGuid(), rowId, 1), Number(Guid.NewGuid(), rowId, 2)]);
        var sameOrdinal = () => ExperienceContent.Create(null, null, [],
            [Number(Guid.NewGuid(), Guid.NewGuid(), 1), Number(Guid.NewGuid(), Guid.NewGuid(), 1)]);
        inconsistent.Should().Throw<ArgumentException>();
        sameOrdinal.Should().Throw<ArgumentException>();
        ExperienceContent.Create(null, null, [], Enumerable.Range(1, 12)
            .Select(ordinal => Number(Guid.NewGuid(), Guid.NewGuid(), ordinal))).Observations.Should().HaveCount(12);
    }

    [Fact]
    public void ObservationBoundStopsOverlargeInput()
    {
        ExperienceContent.Create(null, null, [], Enumerable.Range(0, 200).Select(_ => Number(Guid.NewGuid())))
            .Observations.Should().HaveCount(200);
        var act = () => ExperienceContent.Create(null, null, [], Enumerable.Range(0, 201).Select(_ => Number(Guid.NewGuid())));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ContentCopiesCallerCollectionsAndPreservesNullAndZeroInPayload()
    {
        var tags = new[] { "original" };
        var observations = new[] { Number(Guid.NewGuid()) };
        var content = ExperienceContent.Create(null, null, tags, observations);
        tags[0] = "changed";
        observations[0] = Number(Guid.NewGuid());
        content.Tags[0].Should().Be("original");
        content.Observations[0].Should().NotBe(observations[0]);
        var target = ExperienceTarget.Create(Guid.NewGuid(), null, null, "label");
        var date = ExperienceOccurrence.Create(new DateOnly(2026, 10, 9), ExperienceTimePrecision.DateOnly);
        var snapshotId = Guid.NewGuid();
        var payload = content.CanonicalPayload(target, date, snapshotId, new string('a', 64));
        content.CanonicalPayload(target, date, snapshotId, new string('a', 64)).Should().Be(payload);
        using var document = JsonDocument.Parse(payload);
        document.RootElement.GetProperty("sessionScore").ValueKind.Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty("occurrence").GetProperty("localTime").ValueKind.Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty("observations")[0].GetProperty("decimalValue").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public void CompletePayloadEnforcesUtf8LimitBeyondIndividualFieldBounds()
    {
        var values = Enumerable.Range(0, 70).Select(_ => ExperienceObservationValue.Create(Guid.NewGuid(),
            ProductExperienceObservationRole.Observation, ProductExperienceObservationValueType.Text, textValue: new string('x', 2000)));
        var content = ExperienceContent.Create(null, null, [], values);
        var act = () => content.CanonicalPayload(ExperienceTarget.Create(Guid.NewGuid(), null, null, "label"),
            ExperienceOccurrence.Create(new DateOnly(2026, 10, 9), ExperienceTimePrecision.DateOnly), Guid.NewGuid(), new string('a', 64));
        act.Should().Throw<ArgumentException>();
    }

    private static ExperienceObservationValue Number(Guid definitionId, Guid? rowId = null, int? ordinal = null)
        => ExperienceObservationValue.Create(definitionId, ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Decimal, decimalValue: 0m, rowId: rowId, rowOrdinal: ordinal);
}
