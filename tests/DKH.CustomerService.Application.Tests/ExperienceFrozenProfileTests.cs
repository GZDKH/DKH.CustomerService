using DKH.CustomerService.Domain.Entities.ExperienceJournal;
using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ExperienceFrozenProfileTests
{
    private static readonly Guid Catalog = new("861bcc3b-1608-4b48-b59f-d8d73c1c7f11");
    private static readonly Guid Category = new("67b3d4ea-203f-4f6e-8c8c-81ebf1e60b70");
    private static readonly Guid Definition = new("16be7e14-2d3e-4c86-8f0f-1d5b8a25b253");

    [Fact]
    public void FrozenMetadataRoundtripsAndCopiesCallerCollections()
    {
        var units = new[] { new ExperienceFrozenUnit("kg", "kilograms", 1, 0), new ExperienceFrozenUnit("g", "grams", 0.001m, 0) };
        var option = new ExperienceFrozenOption(Guid.NewGuid(), "two kilograms", ProductExperienceObservationValueType.Decimal,
            null, 2m, null, null);
        var options = new[] { option };
        var definition = Numeric(units, options, 1, 3);
        var definitions = new[] { definition };
        var profile = ExperienceProfileSnapshotEntity.CreateFrozen(Catalog, Category, "category", 3, definitions);
        var original = profile.ReadCanonicalSchema();
        units[0] = new ExperienceFrozenUnit("kg", "changed label", 1, 0);
        options[0] = new ExperienceFrozenOption(option.Id, "changed label", ProductExperienceObservationValueType.Decimal, null, 2m, null, null);
        definitions[0] = Numeric([], [], null, null);
        profile.ReadCanonicalSchema().Should().Be(original);
        profile.SchemaHash.Should().Be(ExperienceCanonicalJson.Hash(original));
        var retained = profile.ReadDefinitions().Single();
        retained.Label.Should().Be("Weight");
        retained.CatalogId.Should().Be(Catalog);
        retained.CategoryId.Should().Be(Category);
        retained.Units.Single(unit => unit.Code == "kg").Label.Should().Be("kilograms");
        retained.Options.Single().Label.Should().Be("two kilograms");
        retained.Minimum.Should().Be(1m);
        retained.Maximum.Should().Be(3m);
        Action mutate = () => ((IList<ExperienceFrozenUnit>)retained.Units).Add(units[0]);
        mutate.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void EquivalentResolvedOrderingHasTheSameCanonicalHash()
    {
        var a = new ExperienceFrozenUnit("kg", "kilograms", 1, 0);
        var b = new ExperienceFrozenUnit("g", "grams", 0.001m, 0);
        var first = ExperienceProfileSnapshotEntity.CreateFrozen(Catalog, Category, "category", 1, [Numeric([a, b], [], null, null)]);
        var second = ExperienceProfileSnapshotEntity.CreateFrozen(Catalog, Category, "category", 1, [Numeric([b, a], [], null, null)]);
        second.SchemaHash.Should().Be(first.SchemaHash);
        second.ReadCanonicalSchema().Should().Be(first.ReadCanonicalSchema());
    }

    [Fact]
    public void GeneralUsesTheSameProfileValidatorWithoutCatalogOrDefinitions()
    {
        var general = ExperienceProfileSnapshotEntity.CreateGeneralV1();
        general.ReadDefinitions().Should().BeEmpty();
        general.ValidateObservations([], null, null);
        var act = () => general.ValidateObservations([Number(0m)], null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExactApprovedConversionAndNumericBoundsAreValidatedWithoutChangingAuthorValue()
    {
        var profile = Profile(Numeric([new("kg", "kilograms", 1, 0), new("g", "grams", 0.001m, 0)], [], 1, 2));
        var observed = Number(1500m, "g");
        profile.ValidateObservations([observed], Catalog, Category);
        observed.DecimalValue.Should().Be(1500m);
        observed.UnitCode.Should().Be("g");
        foreach (var invalid in new[] { Number(999m, "g"), Number(2001m, "g"), Number(1m, "lb"), Number(1m) })
        {
            var act = () => profile.ValidateObservations([invalid], Catalog, Category);
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void OffsetConversionAndLossyOrOverflowingNumericConversionAreHandledExplicitly()
    {
        var temperature = new ExperienceFrozenDefinition(Definition, Catalog, Category, "Temperature",
            ProductExperienceObservationRole.Observation, ProductExperienceObservationValueType.Decimal, false, "K",
            [new("K", "kelvin", 1, 0), new("C", "Celsius", 1, 273.15m)], [], minimum: 273.15m, maximum: 373.15m);
        Profile(temperature).ValidateObservations([Number(0m, "C")], Catalog, Category);
        var tiny = Profile(Numeric([new("kg", "kilograms", 1, 0), new("g", "grams", 0.001m, 0)], [], null, null));
        var lossy = () => tiny.ValidateObservations([Number(0.000001m, "g")], Catalog, Category);
        lossy.Should().Throw<ArgumentException>();
        var overflowing = new ExperienceFrozenUnit("overflow", "overflow", decimal.MaxValue, 0);
        var overflow = () => overflowing.ToCanonical(2m);
        overflow.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SelectedOptionsRequireTheRetainedIdentityTypeAndExactValue()
    {
        var option = new ExperienceFrozenOption(Guid.NewGuid(), "two kilograms", ProductExperienceObservationValueType.Decimal, null, 2m, null, null);
        var definition = Numeric([new("kg", "kilograms", 1, 0), new("g", "grams", 0.001m, 0)], [option], null, null, false);
        var profile = Profile(definition);
        profile.ValidateObservations([Number(2000m, "g", option.Id)], Catalog, Category);
        foreach (var invalid in new[] { Number(2m, "kg"), Number(1m, "kg", option.Id), Number(2m, "kg", Guid.NewGuid()) })
        {
            var act = () => profile.ValidateObservations([invalid], Catalog, Category);
            act.Should().Throw<ArgumentException>();
        }

        var emptyOption = () => Number(2m, "kg", Guid.Empty);
        emptyOption.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UndeclaredWrongTypeWrongRoleAndReadOnlyContextCannotBeWritten()
    {
        var profile = Profile(Numeric([], [], null, null));
        var foreign = ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Decimal, decimalValue: 1m);
        var wrongType = ExperienceObservationValue.Create(Definition, ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Boolean, booleanValue: false);
        var wrongRole = ExperienceObservationValue.Create(Definition, ProductExperienceObservationRole.RecommendedPreparation,
            ProductExperienceObservationValueType.Decimal, decimalValue: 1m);
        foreach (var invalid in new[] { foreign, wrongType, wrongRole })
        {
            var act = () => profile.ValidateObservations([invalid], Catalog, Category);
            act.Should().Throw<ArgumentException>();
        }

        var context = new ExperienceFrozenDefinition(Definition, Catalog, Category, "Fact",
            ProductExperienceObservationRole.Observation, ProductExperienceObservationValueType.Decimal, true, null, [], []);
        var writeFact = () => Profile(context).ValidateObservations([Number(1m)], Catalog, Category);
        writeFact.Should().Throw<ArgumentException>();
        var defaults = new ExperienceFrozenDefinition(Definition, Catalog, Category, "Recommendation default",
            ProductExperienceObservationRole.RecommendedPreparation, ProductExperienceObservationValueType.Decimal, true, null, [], []);
        var writeDefault = () => Profile(defaults).ValidateObservations([wrongRole], Catalog, Category);
        writeDefault.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SnapshotAndObservationRequireExactCatalogCategoryBindings()
    {
        var definition = Numeric([], [], null, null);
        var profile = Profile(definition);
        void foreignCategory() => profile.ValidateObservations([Number(1m)], Catalog, Guid.NewGuid());
        void foreignCatalog() => profile.ValidateObservations([Number(1m)], Guid.NewGuid(), Category);
        void absent() => profile.ValidateObservations([Number(1m)], null, null);
        void binding() => ExperienceProfileSnapshotEntity.CreateFrozen(Catalog, Guid.NewGuid(), "category", 1, [definition]);
        void noCatalog() => ExperienceProfileSnapshotEntity.CreateFrozen(Guid.Empty, Category, "category", 1, [definition]);
        void duplicate() => ExperienceProfileSnapshotEntity.CreateFrozen(Catalog, Category, "category", 1, [definition, definition]);
        foreach (var act in new[] { foreignCategory, foreignCatalog, absent, binding, noCatalog, duplicate })
        {
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void InvalidResolvedUnitsOptionsAndBoundsAreRejectedBeforeFreezing()
    {
        var unit = new ExperienceFrozenUnit("kg", "kilograms", 1, 0);
        var option = new ExperienceFrozenOption(Guid.NewGuid(), "false", ProductExperienceObservationValueType.Boolean, null, null, null, false);
        void duplicateUnits() => Numeric([unit, unit], [], null, null);
        void missingCanonical() => Numeric([new("g", "grams", 0.001m, 0)], [], null, null);
        void wrongOption() => Numeric([], [option], null, null);
        void bounds() => Numeric([], [], 2, 1);
        void textUnits() => _ = new ExperienceFrozenDefinition(Definition, Catalog, Category, "Text",
            ProductExperienceObservationRole.Observation, ProductExperienceObservationValueType.Text, false, "kg", [unit], []);
        foreach (var act in new[] { duplicateUnits, missingCanonical, wrongOption, bounds, textUnits })
        {
            act.Should().Throw<ArgumentException>();
        }
    }

    private static ExperienceProfileSnapshotEntity Profile(ExperienceFrozenDefinition definition)
        => ExperienceProfileSnapshotEntity.CreateFrozen(Catalog, Category, "category", 1, [definition]);

    private static ExperienceFrozenDefinition Numeric(ExperienceFrozenUnit[] units, ExperienceFrozenOption[] options,
        decimal? minimum, decimal? maximum, bool allowCustom = true)
        => new(Definition, Catalog, Category, "Weight", ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Decimal, false, units.Length == 0 ? null : "kg", units, options, allowCustom, minimum, maximum);

    private static ExperienceObservationValue Number(decimal value, string? unit = null, Guid? option = null)
        => ExperienceObservationValue.Create(Definition, ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Decimal, decimalValue: value, unitCode: unit, optionId: option);
}
