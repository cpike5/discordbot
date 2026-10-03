using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Helpers;

public class FlaggedEventReviewTests
{
    [Theory]
    [InlineData(FlaggedEventStatus.Pending, true)]
    [InlineData(FlaggedEventStatus.Acknowledged, true)]
    [InlineData(FlaggedEventStatus.Dismissed, false)]
    [InlineData(FlaggedEventStatus.Actioned, false)]
    public void Dismiss_IsOfferedForOpenEventsOnly(FlaggedEventStatus status, bool expected)
    {
        FlaggedEventReviewRules.CanApply(FlaggedEventReviewAction.Dismiss, status).Should().Be(expected);
    }

    [Theory]
    [InlineData(FlaggedEventStatus.Pending, true)]
    [InlineData(FlaggedEventStatus.Acknowledged, false)]
    [InlineData(FlaggedEventStatus.Dismissed, false)]
    [InlineData(FlaggedEventStatus.Actioned, false)]
    public void Acknowledge_IsOfferedForPendingEventsOnly(FlaggedEventStatus status, bool expected)
    {
        FlaggedEventReviewRules.CanApply(FlaggedEventReviewAction.Acknowledge, status).Should().Be(expected);
    }

    [Theory]
    [InlineData(FlaggedEventStatus.Pending, true)]
    [InlineData(FlaggedEventStatus.Acknowledged, true)]
    [InlineData(FlaggedEventStatus.Dismissed, false)]
    [InlineData(FlaggedEventStatus.Actioned, false)]
    public void RecordOutcome_IsOfferedForOpenEvents_IncludingAcknowledgedOnes(FlaggedEventStatus status, bool expected)
    {
        FlaggedEventReviewRules.CanRecordOutcome(status).Should().Be(expected);
    }

    [Fact]
    public void Describe_AllDone_IsASuccess()
    {
        var outcome = new FlaggedEventBatchOutcome { Done = 12 };

        var (kind, message) = outcome.Describe(FlaggedEventReviewAction.Dismiss, 12);

        kind.Should().Be(FlaggedEventBatchOutcome.ToastKind.Success);
        message.Should().Be("12 events dismissed.");
    }

    [Fact]
    public void Describe_OneEvent_ReadsAsOne()
    {
        var (_, message) = new FlaggedEventBatchOutcome { Done = 1 }.Describe(FlaggedEventReviewAction.Acknowledge, 1);

        message.Should().Be("Event acknowledged.");
    }

    [Fact]
    public void Describe_PartialFailure_IsAWarningThatSaysWhatWasLeftAndWhy()
    {
        var outcome = new FlaggedEventBatchOutcome { Done = 12, Skipped = 2, Failed = 1 };

        var (kind, message) = outcome.Describe(FlaggedEventReviewAction.Dismiss, 15);

        kind.Should().Be(FlaggedEventBatchOutcome.ToastKind.Warning);
        message.Should().Be("Dismissed 12 of 15 events. 2 already reviewed, 1 could not be updated.");
    }

    [Fact]
    public void Describe_NothingChanged_IsAnError()
    {
        var outcome = new FlaggedEventBatchOutcome { Skipped = 3 };

        var (kind, message) = outcome.Describe(FlaggedEventReviewAction.Acknowledge, 3);

        kind.Should().Be(FlaggedEventBatchOutcome.ToastKind.Error);
        message.Should().Be("No events were acknowledged. 3 already reviewed.");
    }

    [Fact]
    public void Describe_EventsThatNoLongerExist_AreNamed()
    {
        var outcome = new FlaggedEventBatchOutcome { Done = 1, NotFound = 1 };

        var (_, message) = outcome.Describe(FlaggedEventReviewAction.Dismiss, 2);

        message.Should().Contain("1 no longer exists");
    }

    [Fact]
    public void Describe_NothingToDo_DoesNotThrow()
    {
        var (kind, message) = new FlaggedEventBatchOutcome().Describe(FlaggedEventReviewAction.Dismiss, 0);

        kind.Should().Be(FlaggedEventBatchOutcome.ToastKind.Error);
        message.Should().NotBeNullOrWhiteSpace();
    }
}
