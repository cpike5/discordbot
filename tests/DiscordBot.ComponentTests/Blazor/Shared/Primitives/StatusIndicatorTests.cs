using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class StatusIndicatorTests : BlazorComponentTestContext
{
    [Fact]
    public void DotOnly_RendersNoOuterWrapper_JustTheDot()
    {
        var cut = Render<StatusIndicator>(p => p
            .Add(x => x.Status, StatusType.Online)
            .Add(x => x.DisplayStyle, StatusDisplayStyle.DotOnly));

        var span = cut.Find("span");
        span.ClassList.Should().Contain("bg-success").And.Contain("rounded-full");
        cut.Markup.Should().NotContain("Online");
    }

    [Theory]
    [InlineData(StatusType.Online, "bg-success", "Online")]
    [InlineData(StatusType.Idle, "bg-warning", "Idle")]
    [InlineData(StatusType.Busy, "bg-error", "Do Not Disturb")]
    [InlineData(StatusType.Offline, "bg-text-tertiary", "Offline")]
    public void DotWithText_Default_UsesStatusColorAndDefaultLabel(StatusType status, string colorClass, string label)
    {
        var cut = Render<StatusIndicator>(p => p.Add(x => x.Status, status));

        cut.Markup.Should().Contain(label);
        cut.Find($"span.{colorClass}").Should().NotBeNull();
    }

    [Fact]
    public void Text_OverridesDefaultLabel()
    {
        var cut = Render<StatusIndicator>(p => p.Add(x => x.Status, StatusType.Online).Add(x => x.Text, "Custom"));
        cut.Markup.Should().Contain("Custom").And.NotContain("Online<");
    }

    [Fact]
    public void BadgeStyle_RendersPillWithBadgeBackground()
    {
        var cut = Render<StatusIndicator>(p => p
            .Add(x => x.Status, StatusType.Online)
            .Add(x => x.DisplayStyle, StatusDisplayStyle.BadgeStyle));

        var root = cut.Find("span");
        root.ClassList.Should().Contain("rounded-full").And.Contain("bg-success/20");
    }

    [Fact]
    public void IsPulsing_RendersAnimatePingElement()
    {
        var cut = Render<StatusIndicator>(p => p.Add(x => x.Status, StatusType.Online).Add(x => x.IsPulsing, true));
        cut.Find("span.animate-ping").Should().NotBeNull();
    }

    [Fact]
    public void IsPulsing_False_DoesNotRenderAnimatePingElement()
    {
        var cut = Render<StatusIndicator>(p => p.Add(x => x.Status, StatusType.Online).Add(x => x.IsPulsing, false));
        cut.FindAll("span.animate-ping").Should().BeEmpty();
    }

    [Theory]
    [InlineData(StatusSize.Small, "w-1.5")]
    [InlineData(StatusSize.Medium, "w-2")]
    [InlineData(StatusSize.Large, "w-3")]
    public void Size_AppliesExpectedDotClass(StatusSize size, string expectedClass)
    {
        var cut = Render<StatusIndicator>(p => p
            .Add(x => x.Status, StatusType.Online)
            .Add(x => x.DisplayStyle, StatusDisplayStyle.DotOnly)
            .Add(x => x.Size, size));

        cut.Find("span").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void Class_And_AdditionalAttributes_LandOnRootElement_ForEachDisplayStyle()
    {
        foreach (var style in new[] { StatusDisplayStyle.DotOnly, StatusDisplayStyle.DotWithText, StatusDisplayStyle.BadgeStyle })
        {
            var cut = Render<StatusIndicator>(p => p
                .Add(x => x.Status, StatusType.Online)
                .Add(x => x.DisplayStyle, style)
                .Add(x => x.Class, "extra")
                .AddUnmatched("data-testid", "status"));

            var root = cut.Find("span");
            root.ClassList.Should().Contain("extra");
            root.GetAttribute("data-testid").Should().Be("status");
        }
    }
}
