using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class ExperienceOccurrenceTests
{
    [Fact]
    public void DateOnly_RetainsTheChosenDateWithoutInventedTime()
    {
        var date = new DateOnly(2025, 12, 31);
        var result = ExperienceOccurrence.Create(date, ExperienceTimePrecision.DateOnly);
        result.OccurredDate.Should().Be(date);
        result.LocalTime.Should().BeNull();
        result.LocalTimeIso.Should().BeNull();
        result.TimeZone.Should().BeNull();
        result.UtcOffsetMinutes.Should().BeNull();
    }

    [Theory]
    [InlineData(-240)]
    [InlineData(-300)]
    public void AmbiguousDst_RetainsEitherExplicitChosenOffset(int offset)
    {
        var date = new DateOnly(2025, 11, 2);
        var time = new TimeOnly(1, 30);
        var result = ExperienceOccurrence.Create(date, ExperienceTimePrecision.LocalTime, time, "America/New_York", offset);
        result.OccurredDate.Should().Be(date);
        result.LocalTime.Should().Be(time);
        result.TimeZone.Should().Be("America/New_York");
        result.UtcOffsetMinutes.Should().Be(offset);
    }

    [Fact]
    public void LocalTime_RetainsAllTicksWithoutPostgresqlTimeRounding()
    {
        var time = new TimeOnly(14, 5, 6).Add(TimeSpan.FromTicks(1234567));
        var result = ExperienceOccurrence.Create(new DateOnly(2025, 10, 9), ExperienceTimePrecision.LocalTime, time, "Asia/Kolkata", 330);
        result.LocalTime.Should().Be(time);
        result.LocalTimeIso.Should().Be("14:05:06.1234567");
    }

    [Fact]
    public void Utc_LocalMidnightRemainsAnExplicitTimedChoice()
    {
        var result = ExperienceOccurrence.Create(new DateOnly(2025, 1, 1), ExperienceTimePrecision.LocalTime, TimeOnly.MinValue, "UTC", 0);
        result.Precision.Should().Be(ExperienceTimePrecision.LocalTime);
        result.LocalTimeIso.Should().Be("00:00:00.0000000");
        result.UtcOffsetMinutes.Should().Be(0);
    }

    [Theory]
    [InlineData("Not/AnIanaZone")]
    [InlineData("Eastern Standard Time")]
    [InlineData(" UTC")]
    [InlineData("")]
    public void InvalidOrWindowsTimezone_IsRejected(string zone)
    {
        var act = () => ExperienceOccurrence.Create(new DateOnly(2025, 1, 1), ExperienceTimePrecision.LocalTime, new TimeOnly(12, 0), zone, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NonexistentDstTime_IsRejected()
    {
        var act = () => ExperienceOccurrence.Create(new DateOnly(2025, 3, 9), ExperienceTimePrecision.LocalTime, new TimeOnly(2, 30), "America/New_York", -300);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-360)]
    [InlineData(0)]
    [InlineData(1000000)]
    public void OffsetNotAmongAmbiguousChoices_IsRejected(int offset)
    {
        var act = () => ExperienceOccurrence.Create(new DateOnly(2025, 11, 2), ExperienceTimePrecision.LocalTime, new TimeOnly(1, 30), "America/New_York", offset);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DateOnly_RejectsEveryOptionalTimeFieldEvenZeroOffset()
    {
        var date = new DateOnly(2025, 1, 1);
        Action time = () => ExperienceOccurrence.Create(date, ExperienceTimePrecision.DateOnly, TimeOnly.MinValue);
        Action zone = () => ExperienceOccurrence.Create(date, ExperienceTimePrecision.DateOnly, timeZone: "UTC");
        Action offset = () => ExperienceOccurrence.Create(date, ExperienceTimePrecision.DateOnly, utcOffsetMinutes: 0);
        time.Should().Throw<ArgumentException>();
        zone.Should().Throw<ArgumentException>();
        offset.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Timed_RequiresTimeZoneAndOffsetWithoutDefaults()
    {
        var date = new DateOnly(2025, 1, 1);
        Action time = () => ExperienceOccurrence.Create(date, ExperienceTimePrecision.LocalTime, timeZone: "UTC", utcOffsetMinutes: 0);
        Action zone = () => ExperienceOccurrence.Create(date, ExperienceTimePrecision.LocalTime, TimeOnly.MinValue, utcOffsetMinutes: 0);
        Action offset = () => ExperienceOccurrence.Create(date, ExperienceTimePrecision.LocalTime, TimeOnly.MinValue, "UTC");
        time.Should().Throw<ArgumentException>();
        zone.Should().Throw<ArgumentException>();
        offset.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UndefinedPrecision_IsRejected()
    {
        var act = () => ExperienceOccurrence.Create(new DateOnly(2025, 1, 1), 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
