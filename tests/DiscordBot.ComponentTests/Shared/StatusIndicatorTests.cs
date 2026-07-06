using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class StatusIndicatorTests : TestContext
{
    [Fact]
    public void RendersDotWithText_WithStatusColorAndDefaultLabel()
    {
        var cut = RenderComponent<StatusIndicator>(p => p
            .Add(x => x.Status, StatusType.Online));

        cut.Markup.Should().Contain("bg-success");
        cut.Find("span").ClassList.Should().Contain("text-success");
        cut.Markup.Should().Contain("Online");
    }

    [Fact]
    public void RendersDotOnly_WithoutText()
    {
        var cut = RenderComponent<StatusIndicator>(p => p
            .Add(x => x.Status, StatusType.Busy)
            .Add(x => x.DisplayStyle, StatusDisplayStyle.DotOnly));

        cut.Markup.Should().Contain("bg-error");
        cut.Markup.Should().NotContain("Do Not Disturb");
    }

    [Fact]
    public void RendersBadgeStyle_WithPillClasses()
    {
        var cut = RenderComponent<StatusIndicator>(p => p
            .Add(x => x.Status, StatusType.Idle)
            .Add(x => x.DisplayStyle, StatusDisplayStyle.BadgeStyle)
            .Add(x => x.Text, "Away"));

        var pill = cut.Find("span");
        pill.ClassList.Should().Contain(new[] { "rounded-full", "bg-warning/20", "text-warning" });
        pill.TextContent.Should().Contain("Away");
    }

    [Fact]
    public void AddsPingElement_WhenPulsing()
    {
        var pulsing = RenderComponent<StatusIndicator>(p => p
            .Add(x => x.Status, StatusType.Online)
            .Add(x => x.IsPulsing, true));
        pulsing.Markup.Should().Contain("animate-ping");

        var still = RenderComponent<StatusIndicator>(p => p
            .Add(x => x.Status, StatusType.Online));
        still.Markup.Should().NotContain("animate-ping");
    }
}
