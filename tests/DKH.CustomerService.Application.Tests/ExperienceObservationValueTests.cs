using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ExperienceObservationValueTests
{
    [Fact]
    public void ExactDecimalAndFalse_RemainPresent()
    {
        var number = ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Decimal, decimalValue: 0m);
        var flag = ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Boolean, booleanValue: false);
        number.DecimalValue.Should().Be(0m);
        number.BooleanValue.Should().BeNull();
        flag.BooleanValue.Should().BeFalse();
        flag.DecimalValue.Should().BeNull();
    }

    [Fact]
    public void DecimalBoundariesAndTrailingZeroScale_AreLossless()
    {
        foreach (var value in new[] { -ExperienceObservationValue.MaximumDecimal, ExperienceObservationValue.MaximumDecimal, 1.0000000m })
        {
            var result = ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
                ProductExperienceObservationValueType.Decimal, decimalValue: value);
            result.DecimalValue.Should().Be(value);
        }
    }

    [Fact]
    public void OutOfRangeOrLossyDecimal_IsRejected()
    {
        foreach (var value in new[] { decimal.MinValue, decimal.MaxValue, 1000000000000m, -1000000000000m, 0.0000001m, -0.0000001m })
        {
            var act = () => ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
                ProductExperienceObservationValueType.Decimal, decimalValue: value);
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void MissingMultipleAndWrongTypedValues_AreRejected()
    {
        Action missing = () => ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation, ProductExperienceObservationValueType.Decimal);
        Action multiple = () => ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Decimal, decimalValue: 0m, booleanValue: false);
        Action wrong = () => ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Integer, decimalValue: 0m);
        missing.Should().Throw<ArgumentException>();
        multiple.Should().Throw<ArgumentException>();
        wrong.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RowIdentityAndOrdinal_AreAnExplicitPair()
    {
        var definition = Guid.NewGuid();
        var row = Guid.NewGuid();
        var ordinary = ExperienceObservationValue.Create(definition, ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Integer, integerValue: 0);
        var repeated = ExperienceObservationValue.Create(definition, ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Integer, integerValue: 0, rowId: row, rowOrdinal: 12);
        ordinary.RowId.Should().BeNull();
        ordinary.RowOrdinal.Should().BeNull();
        repeated.RowId.Should().Be(row);
        repeated.RowOrdinal.Should().Be(12);
        foreach (var (id, ordinal) in new (Guid?, int?)[] { (row, null), (null, 1), (Guid.Empty, 1), (row, 0), (row, 13) })
        {
            var act = () => ExperienceObservationValue.Create(definition, ProductExperienceObservationRole.Observation,
                ProductExperienceObservationValueType.Integer, integerValue: 0, rowId: id, rowOrdinal: ordinal);
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void SharedShape_RetainsLegacyDefinitionEnumAndTextUnitBounds()
    {
        static void definition() => ExperienceObservationValue.Create(Guid.Empty, ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Text, textValue: "value");
        static void role() => ExperienceObservationValue.Create(Guid.NewGuid(), 0,
            ProductExperienceObservationValueType.Text, textValue: "value");
        static void type() => ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation, 0, textValue: "value");
        static void text() => ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Text, textValue: new string('x', 2001));
        static void unit() => ExperienceObservationValue.Create(Guid.NewGuid(), ProductExperienceObservationRole.Observation,
            ProductExperienceObservationValueType.Decimal, decimalValue: 0m, unitCode: new string('x', 33));
        foreach (var act in new[] { definition, role, type, text, unit })
        {
            act.Should().Throw<ArgumentException>();
        }
    }
}
