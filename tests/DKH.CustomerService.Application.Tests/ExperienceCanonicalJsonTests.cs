using System.Globalization;
using System.Text;
using System.Text.Json;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ExperienceCanonicalJsonTests
{
    [Fact]
    public void PropertyOrderAndNumericScale_HaveStableCanonicalBytesAndHash()
    {
        var first = ExperienceCanonicalJson.Canonicalize(/*lang=json,strict*/ "{\"z\":false,\"a\":{\"b\":1.0000,\"a\":null}}");
        var second = ExperienceCanonicalJson.Canonicalize(/*lang=json,strict*/ "{\"a\":{\"a\":null,\"b\":1},\"z\":false}");
        first.Should().Be(second);
        ExperienceCanonicalJson.Hash(first).Should().Be(ExperienceCanonicalJson.Hash(second));
        ExperienceCanonicalJson.MatchesHash(first, ExperienceCanonicalJson.Hash(first)).Should().BeTrue();
        ExperienceCanonicalJson.MatchesHash(first + " ", ExperienceCanonicalJson.Hash(first)).Should().BeFalse();
    }

    [Fact]
    public void MissingNullZeroFalseAndArrayOrder_RemainDistinct()
    {
        var payloads = new[] { /*lang=json,strict*/ "{}", /*lang=json,strict*/ "{\"v\":null}", /*lang=json,strict*/ "{\"v\":0}",
            /*lang=json,strict*/ "{\"v\":false}", /*lang=json,strict*/ "[1,2]", /*lang=json,strict*/ "[2,1]" };
        payloads.Select(value => ExperienceCanonicalJson.Hash(ExperienceCanonicalJson.Canonicalize(value)))
            .Distinct().Should().HaveCount(payloads.Length);
    }

    [Fact]
    public void Utf8Budget_UsesBytesAndPreservesUnicode()
    {
        var text = "Общий 🍵";
        var canonical = ExperienceCanonicalJson.Serialize(new { Note = text });
        using var document = JsonDocument.Parse(canonical);
        document.RootElement.GetProperty("note").GetString().Should().Be(text);
        Encoding.UTF8.GetByteCount(canonical).Should().BeGreaterThan(canonical.Length);
        var action = () => ExperienceCanonicalJson.Serialize(new { Note = new string('я', 70000) });
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DuplicateProperties_AreNotFlattened()
    {
        var action = () => ExperienceCanonicalJson.Canonicalize(/*lang=json,strict*/ "{\"score\":1,\"score\":5}");
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("1e-29")]
    [InlineData("0.33333333333333333333333333333")]
    public void NumericParsing_CannotSilentlyRoundOrUnderflow(string number)
    {
        var action = () => ExperienceCanonicalJson.Canonicalize(number);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DecimalSerialization_IsCultureIndependent()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var result = ExperienceCanonicalJson.Serialize(new { Score = 12.345678m });
            result.Should().Be(/*lang=json,strict*/ "{\"score\":12.345678}");
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }
    }
}
