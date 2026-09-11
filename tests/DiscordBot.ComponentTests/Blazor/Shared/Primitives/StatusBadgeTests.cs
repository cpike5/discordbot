using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class StatusBadgeTests : BlazorComponentTestContext
{
    [Theory]
    [InlineData(FlaggedEventStatus.Pending, "status-pending", "Pending")]
    [InlineData(FlaggedEventStatus.Dismissed, "status-dismissed", "Dismissed")]
    [InlineData(FlaggedEventStatus.Acknowledged, "status-acknowledged", "Acknowledged")]
    [InlineData(FlaggedEventStatus.Actioned, "status-actioned", "Actioned")]
    public void Status_RendersExpectedClassAndLabel(FlaggedEventStatus status, string expectedClass, string expectedLabel)
    {
        var cut = Render<StatusBadge>(p => p.Add(x => x.Status, status));

        var span = cut.Find("span");
        span.ClassList.Should().Contain("status-badge").And.Contain(expectedClass);
        span.TextContent.Trim().Should().Be(expectedLabel);
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<StatusBadge>(p => p
            .Add(x => x.Status, FlaggedEventStatus.Pending)
            .Add(x => x.Class, "extra")
            .AddUnmatched("data-testid", "my-status-badge"));

        var span = cut.Find("span");
        span.ClassList.Should().Contain("extra");
        span.GetAttribute("data-testid").Should().Be("my-status-badge");
    }
}
