using DKH.CustomerService.Application.ExperienceJournal;
using DKH.CustomerService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public sealed class JournalCopyIntentTests
{
    private static JournalCopyIntent Valid() => new(Guid.NewGuid(), 1,
        ExperienceOccurrence.Create(new DateOnly(2026, 10, 12), ExperienceTimePrecision.DateOnly), JournalCopyTargetChoice.Retain);

    [Fact]
    public void ExplicitRetainOrBoundedUnknownFallbackIsRequired()
    {
        var intent = Valid();
        intent.Validate();
        (intent with { TargetChoice = JournalCopyTargetChoice.UnknownFallback, UnknownLabel = "Private fallback" }).Validate();
        foreach (var invalid in new[]
        {
            intent with { SourceEntryId = Guid.Empty }, intent with { SourceRevision = 0 },
            intent with { Occurrence = null! }, intent with { TargetChoice = (JournalCopyTargetChoice)42 },
            intent with { UnknownLabel = "implicit fallback" }, intent with { ProducerLabel = "implicit" },
            intent with { TargetChoice = JournalCopyTargetChoice.UnknownFallback },
            intent with { TargetChoice = JournalCopyTargetChoice.UnknownFallback, UnknownLabel = " " },
            intent with { TargetChoice = JournalCopyTargetChoice.UnknownFallback, UnknownLabel = new string('x', 257) },
            intent with { TargetChoice = JournalCopyTargetChoice.UnknownFallback, UnknownLabel = "private", ProducerLabel = new string('x', 257) },
        })
        {
            var validate = invalid.Validate;
            validate.Should().Throw<ArgumentException>();
        }
    }
}
