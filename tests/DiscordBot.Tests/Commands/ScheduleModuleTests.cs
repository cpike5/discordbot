using DiscordBot.Bot.Commands;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Commands;

/// <summary>
/// Tests for how <c>/schedule-create</c> picks the first run of a new scheduled message.
/// The slash command itself needs a live interaction context, so the first-run resolution it
/// delegates to is tested directly.
/// </summary>
public class ScheduleModuleTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IScheduledMessageService> _service = new();
    private readonly Mock<ITimeParsingService> _parser = new();

    [Fact]
    public async Task ResolveFirstRunAsync_OnceWithStart_UsesTheStartTime()
    {
        var start = Now.AddHours(2);
        _parser.Setup(p => p.Parse("2h", "UTC")).Returns(TimeParseResult.Ok(start, TimeParseType.Relative));

        var (firstRun, error) = await ScheduleModule.ResolveFirstRunAsync(
            ScheduleFrequency.Once, null, "2h", _service.Object, _parser.Object, Now);

        firstRun.Should().Be(start);
        error.Should().BeNull();
    }

    [Fact]
    public async Task ResolveFirstRunAsync_OnceWithoutStart_AsksForOne()
    {
        var (firstRun, error) = await ScheduleModule.ResolveFirstRunAsync(
            ScheduleFrequency.Once, null, null, _service.Object, _parser.Object, Now);

        firstRun.Should().BeNull();
        error.Should().Contain("start time is required");
    }

    [Fact]
    public async Task ResolveFirstRunAsync_StartInThePast_IsRefused()
    {
        _parser.Setup(p => p.Parse("9am", "UTC")).Returns(TimeParseResult.Ok(Now.AddHours(-3), TimeParseType.AbsoluteTime));

        var (firstRun, error) = await ScheduleModule.ResolveFirstRunAsync(
            ScheduleFrequency.Daily, null, "9am", _service.Object, _parser.Object, Now);

        firstRun.Should().BeNull();
        error.Should().Contain("future");
    }

    [Fact]
    public async Task ResolveFirstRunAsync_UnparseableStart_ReturnsTheParserError()
    {
        _parser.Setup(p => p.Parse("whenever", "UTC")).Returns(TimeParseResult.Error("Could not parse 'whenever'"));

        var (firstRun, error) = await ScheduleModule.ResolveFirstRunAsync(
            ScheduleFrequency.Once, null, "whenever", _service.Object, _parser.Object, Now);

        firstRun.Should().BeNull();
        error.Should().Be("Could not parse 'whenever'");
    }

    [Fact]
    public async Task ResolveFirstRunAsync_RecurringWithoutStart_UsesTheNextInterval()
    {
        var next = Now.AddDays(1);
        _service
            .Setup(s => s.CalculateNextExecutionAsync(ScheduleFrequency.Daily, null, null))
            .ReturnsAsync(next);

        var (firstRun, error) = await ScheduleModule.ResolveFirstRunAsync(
            ScheduleFrequency.Daily, null, null, _service.Object, _parser.Object, Now);

        firstRun.Should().Be(next);
        error.Should().BeNull();
    }
}
