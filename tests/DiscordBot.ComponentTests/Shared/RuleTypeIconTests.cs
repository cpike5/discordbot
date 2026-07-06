using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class RuleTypeIconTests : TestContext
{
    [Theory]
    [InlineData(RuleType.Spam, "text-warning", "Spam Detection")]
    [InlineData(RuleType.Content, "text-error", "Content Filter")]
    [InlineData(RuleType.Raid, "text-accent-blue", "Raid Protection")]
    public void RendersColorAndTitle_ForEachRuleType(RuleType type, string expectedColor, string expectedTitle)
    {
        var cut = RenderComponent<RuleTypeIcon>(p => p.Add(x => x.Type, type));

        var svg = cut.Find("svg");
        svg.ClassList.Should().Contain(new[] { "w-5", "h-5", expectedColor });
        svg.GetAttribute("title").Should().Be(expectedTitle);
    }

    [Fact]
    public void RendersPathElement_WithStrokeAttributes()
    {
        var cut = RenderComponent<RuleTypeIcon>(p => p.Add(x => x.Type, RuleType.Spam));

        var path = cut.Find("path");
        path.GetAttribute("stroke-width").Should().Be("2");
        path.GetAttribute("d").Should().NotBeNullOrEmpty();
    }
}
