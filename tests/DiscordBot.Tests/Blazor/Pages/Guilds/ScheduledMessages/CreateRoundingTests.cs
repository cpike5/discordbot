using DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages;
using FluentAssertions;

namespace DiscordBot.Tests.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Unit tests for <see cref="Create.RoundUpToNextFiveMinutes"/>, extracted from the inline math in
/// <c>Create.razor.cs</c>'s <c>OnAfterRenderAsync</c> so it can be tested without controlling
/// <see cref="System.DateTime.UtcNow"/> or <c>BrowserInterop.GetTimeZoneAsync</c>. Covers docs
/// review nit: the original <c>(Minute / 5 + 1) * 5</c> always jumped a full 5 minutes even when
/// already on a 5-minute mark.
/// </summary>
public class CreateRoundingTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(20, 20)]
    [InlineData(55, 55)]
    public void AlreadyOnFiveMinuteMark_StaysUnchanged(int inputMinute, int expectedMinute)
    {
        var value = new DateTime(2026, 1, 1, 9, inputMinute, 0);

        var result = Create.RoundUpToNextFiveMinutes(value);

        result.Should().Be(new DateTime(2026, 1, 1, 9, expectedMinute, 0));
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(4, 5)]
    [InlineData(21, 25)]
    [InlineData(59, 60)]
    public void NotOnFiveMinuteMark_RoundsUpToNextMark(int inputMinute, int expectedTotalMinutesFromHourStart)
    {
        var value = new DateTime(2026, 1, 1, 9, inputMinute, 0);

        var result = Create.RoundUpToNextFiveMinutes(value);

        result.Should().Be(new DateTime(2026, 1, 1, 9, 0, 0).AddMinutes(expectedTotalMinutesFromHourStart));
    }

    [Fact]
    public void RoundingPast59Minutes_RollsIntoNextHour()
    {
        var value = new DateTime(2026, 1, 1, 9, 58, 0);

        var result = Create.RoundUpToNextFiveMinutes(value);

        result.Should().Be(new DateTime(2026, 1, 1, 10, 0, 0));
    }

    [Fact]
    public void SecondsAndSubSecondComponents_AreDiscarded()
    {
        var value = new DateTime(2026, 1, 1, 9, 20, 37, 500);

        var result = Create.RoundUpToNextFiveMinutes(value);

        result.Should().Be(new DateTime(2026, 1, 1, 9, 20, 0));
    }
}
