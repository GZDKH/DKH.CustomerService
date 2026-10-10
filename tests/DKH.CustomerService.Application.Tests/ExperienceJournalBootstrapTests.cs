using System.Reflection;
using System.Text.Json;
using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ExperienceJournalBootstrapTests
{
    [Fact]
    public void GeneralV1IsStableAcrossIndependentInstancesAndHasEmptyDefinitions()
    {
        var first = ExperienceProfileSnapshotEntity.CreateGeneralV1();
        var second = ExperienceProfileSnapshotEntity.CreateGeneralV1();
        first.Id.Should().Be(new Guid("f9eb970a-7e55-54f3-9aaa-74e3383ee0de"));
        second.Id.Should().Be(first.Id);
        first.RendererCode.Should().Be("general");
        first.RendererVersion.Should().Be(1);
        first.SchemaVersion.Should().Be(1);
        first.CatalogId.Should().BeNull();
        first.CategoryId.Should().BeNull();
        first.SchemaHash.Should().Be("f9eb970a7e5524f39aaa74e3383ee0ded5e8f17450a9fc58956ccd0cfe512371");
        second.SchemaHash.Should().Be(first.SchemaHash);
        var json = first.ReadCanonicalSchema();
        second.ReadCanonicalSchema().Should().Be(json);
        ExperienceCanonicalJson.Canonicalize(json).Should().Be(json);
        ExperienceCanonicalJson.Hash(json).Should().Be(first.SchemaHash);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("definitions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void GeneralRejectsUndeclaredObservations()
    {
        var profile = ExperienceProfileSnapshotEntity.CreateGeneralV1();
        profile.ValidateGeneralObservations([]);
        var observation = ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Boolean, booleanValue: false);
        var act = () => profile.ValidateGeneralObservations([observation]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RetainedSchemaHashMismatchFailsClosedWithoutReturningCorruptSchema()
    {
        var profile = ExperienceProfileSnapshotEntity.CreateGeneralV1();
        // Simulate an inconsistent database materialization, rather than an ordinary mutation API.
        typeof(ExperienceProfileSnapshotEntity).GetProperty(nameof(profile.CanonicalSchema))!
            .SetValue(profile, "{}");
        var act = () => profile.ReadCanonicalSchema();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UnknownReferencesAreSeparateAccountLocalLabels()
    {
        var accountId = Guid.NewGuid();
        var first = ExperienceUnknownReferenceEntity.Create(accountId, "Личный неизвестный продукт", "Авторская подпись");
        var second = ExperienceUnknownReferenceEntity.Create(accountId, first.OwnerLabel);
        first.Id.Should().NotBe(Guid.Empty);
        second.Id.Should().NotBe(first.Id);
        first.AccountId.Should().Be(accountId);
        first.OwnerLabel.Should().Be("Личный неизвестный продукт");
        first.ProducerLabel.Should().Be("Авторская подпись");
        second.ProducerLabel.Should().BeNull();
        ExperienceUnknownReferenceEntity.Create(accountId, new string('x', 256), new string('p', 256))
            .OwnerLabel.Length.Should().Be(256);
    }

    [Fact]
    public void UnknownReferenceRequiresAccountAndBoundedNonblankLabel()
    {
        foreach (var (account, label, producer) in new (Guid, string, string?)[]
                 { (Guid.Empty, "label", null), (Guid.NewGuid(), " ", null),
                     (Guid.NewGuid(), new string('x', 257), null), (Guid.NewGuid(), "label", new string('p', 257)) })
        {
            var act = () => ExperienceUnknownReferenceEntity.Create(account, label, producer);
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void SnapshotAndUnknownContentHaveNoPublicSettersOrOrdinaryEditMethods()
    {
        foreach (var type in new[] { typeof(ExperienceProfileSnapshotEntity), typeof(ExperienceUnknownReferenceEntity) })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                (property.SetMethod?.IsPublic ?? false).Should().BeFalse();
            }
        }
    }
}
