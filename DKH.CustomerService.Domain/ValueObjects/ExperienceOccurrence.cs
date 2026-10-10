using System.Globalization;

namespace DKH.CustomerService.Domain.ValueObjects;

public enum ExperienceTimePrecision
{
    DateOnly = 1,
    LocalTime = 2,
}

/// <summary>The author's chosen calendar value, independent of audit timestamps.</summary>
public sealed record ExperienceOccurrence
{
    private ExperienceOccurrence(
        DateOnly occurredDate,
        ExperienceTimePrecision precision,
        TimeOnly? localTime,
        string? timeZone,
        int? utcOffsetMinutes)
    {
        OccurredDate = occurredDate;
        Precision = precision;
        LocalTime = localTime;
        TimeZone = timeZone;
        UtcOffsetMinutes = utcOffsetMinutes;
    }

    public DateOnly OccurredDate { get; }

    public ExperienceTimePrecision Precision { get; }

    public TimeOnly? LocalTime { get; }

    public string? TimeZone { get; }

    public int? UtcOffsetMinutes { get; }

    public string? LocalTimeIso => LocalTime?.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture);

    public static ExperienceOccurrence Create(
        DateOnly occurredDate,
        ExperienceTimePrecision precision,
        TimeOnly? localTime = null,
        string? timeZone = null,
        int? utcOffsetMinutes = null)
    {
        if (!Enum.IsDefined(precision))
        {
            throw new ArgumentOutOfRangeException(nameof(precision));
        }

        if (precision == ExperienceTimePrecision.DateOnly)
        {
            if (localTime.HasValue || timeZone is not null || utcOffsetMinutes.HasValue)
            {
                throw new ArgumentException("Date-only experiences cannot contain time, timezone or offset.");
            }

            return new ExperienceOccurrence(occurredDate, precision, null, null, null);
        }

        if (!localTime.HasValue || !utcOffsetMinutes.HasValue || string.IsNullOrEmpty(timeZone)
            || timeZone.Length > 128 || timeZone.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Timed experiences require local time, IANA timezone and chosen offset.");
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new ArgumentException("A valid IANA timezone is required.", nameof(timeZone));
        }

        if (!zone.HasIanaId && timeZone != "UTC")
        {
            throw new ArgumentException("A valid IANA timezone is required.", nameof(timeZone));
        }

        var local = occurredDate.ToDateTime(localTime.Value, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            throw new ArgumentException("The selected local time does not exist in this timezone.", nameof(localTime));
        }

        var selectedOffset = TimeSpan.FromMinutes(utcOffsetMinutes.Value);
        var validOffsets = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local)
            : [zone.GetUtcOffset(local)];
        if (!validOffsets.Contains(selectedOffset))
        {
            throw new ArgumentException("The selected offset does not match the local time and timezone.", nameof(utcOffsetMinutes));
        }

        return new ExperienceOccurrence(occurredDate, precision, localTime, timeZone, utcOffsetMinutes);
    }
}
