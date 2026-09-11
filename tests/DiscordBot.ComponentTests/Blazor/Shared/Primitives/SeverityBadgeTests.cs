using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class SeverityBadgeTests : BlazorComponentTestContext
{
    [Theory]
    [InlineData(Severity.Low, "severity-low", "Low")]
    [InlineData(Severity.Medium, "severity-medium", "Medium")]
    [InlineData(Severity.High, "severity-high", "High")]
    [InlineData(Severity.Critical, "severity-critical", "Critical")]
    public void Severity_RendersExpectedClassAndLabel(Severity severity, string expectedClass, string expectedLabel)
    {
        var cut = Render<SeverityBadge>(p => p.Add(x => x.Severity, severity));

        var span = cut.Find("span");
        span.ClassList.Should().Contain("severity-badge").And.Contain(expectedClass);
        span.TextContent.Trim().Should().Be(expectedLabel);
    }

    [Fact]
    public void Critical_RendersPulseDot()
    {
        var cut = Render<SeverityBadge>(p => p.Add(x => x.Severity, Severity.Critical));
        cut.Find("span.pulse-dot").Should().NotBeNull();
    }

    [Theory]
    [InlineData(Severity.Low)]
    [InlineData(Severity.Medium)]
    [InlineData(Severity.High)]
    public void NonCritical_DoesNotRenderPulseDot(Severity severity)
    {
        var cut = Render<SeverityBadge>(p => p.Add(x => x.Severity, severity));
        cut.FindAll("span.pulse-dot").Should().BeEmpty();
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<SeverityBadge>(p => p
            .Add(x => x.Severity, Severity.Low)
            .Add(x => x.Class, "extra")
            .AddUnmatched("data-testid", "my-severity-badge"));

        var span = cut.Find("span");
        span.ClassList.Should().Contain("extra");
        span.GetAttribute("data-testid").Should().Be("my-severity-badge");
    }
}
