using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class StatusBadgeTests : TestContext
{
    [Theory]
    [InlineData(FlaggedEventStatus.Pending, "status-pending", "Pending")]
    [InlineData(FlaggedEventStatus.Dismissed, "status-dismissed", "Dismissed")]
    [InlineData(FlaggedEventStatus.Acknowledged, "status-acknowledged", "Acknowledged")]
    [InlineData(FlaggedEventStatus.Actioned, "status-actioned", "Actioned")]
    public void RendersClassAndLabel_ForEachStatus(FlaggedEventStatus status, string expectedClass, string expectedLabel)
    {
        var cut = RenderComponent<StatusBadge>(p => p.Add(x => x.Status, status));

        var span = cut.Find("span");
        span.ClassList.Should().Contain(new[] { "status-badge", expectedClass });
        span.TextContent.Trim().Should().Be(expectedLabel);
    }

    [Fact]
    public void RendersSingleSpan_WithNoExtraMarkup()
    {
        var cut = RenderComponent<StatusBadge>(p => p.Add(x => x.Status, FlaggedEventStatus.Pending));

        cut.FindAll("span").Should().HaveCount(1);
    }
}
