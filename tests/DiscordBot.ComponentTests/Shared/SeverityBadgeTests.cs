using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class SeverityBadgeTests : TestContext
{
    [Theory]
    [InlineData(Severity.Low, "severity-low", "Low")]
    [InlineData(Severity.Medium, "severity-medium", "Medium")]
    [InlineData(Severity.High, "severity-high", "High")]
    [InlineData(Severity.Critical, "severity-critical", "Critical")]
    public void RendersClassAndLabel_ForEachSeverity(Severity severity, string expectedClass, string expectedLabel)
    {
        var cut = RenderComponent<SeverityBadge>(p => p.Add(x => x.Severity, severity));

        var span = cut.Find("span.severity-badge");
        span.ClassList.Should().Contain(expectedClass);
        span.TextContent.Trim().Should().Be(expectedLabel);
    }

    [Fact]
    public void ShowsPulseDot_OnlyForCritical()
    {
        var critical = RenderComponent<SeverityBadge>(p => p.Add(x => x.Severity, Severity.Critical));
        critical.FindAll("span.pulse-dot").Should().HaveCount(1);

        var high = RenderComponent<SeverityBadge>(p => p.Add(x => x.Severity, Severity.High));
        high.FindAll("span.pulse-dot").Should().BeEmpty();
    }
}
