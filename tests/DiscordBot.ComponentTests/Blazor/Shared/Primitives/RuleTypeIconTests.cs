using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class RuleTypeIconTests : BlazorComponentTestContext
{
    [Theory]
    [InlineData(RuleType.Spam, "text-warning")]
    [InlineData(RuleType.Content, "text-error")]
    [InlineData(RuleType.Raid, "text-accent-blue")]
    public void RuleType_AppliesExpectedColorClass(RuleType ruleType, string expectedColorClass)
    {
        var cut = Render<RuleTypeIcon>(p => p.Add(x => x.RuleType, ruleType));
        cut.Find("svg").ClassList.Should().Contain(expectedColorClass).And.Contain("w-5").And.Contain("h-5");
    }

    [Theory]
    [InlineData(RuleType.Spam, "Spam Detection")]
    [InlineData(RuleType.Content, "Content Filter")]
    [InlineData(RuleType.Raid, "Raid Protection")]
    public void RuleType_RendersTitleElement_ForTooltip(RuleType ruleType, string expectedTitle)
    {
        var cut = Render<RuleTypeIcon>(p => p.Add(x => x.RuleType, ruleType));

        cut.Find("svg title").TextContent.Should().Be(expectedTitle);
        cut.Find("svg").GetAttribute("aria-hidden").Should().Be("false");
    }

    [Fact]
    public void UnmappedRuleType_FallsBackToUnknownRule()
    {
        var cut = Render<RuleTypeIcon>(p => p.Add(x => x.RuleType, (RuleType)99));

        cut.Find("svg title").TextContent.Should().Be("Unknown Rule");
        cut.Find("svg").ClassList.Should().Contain("text-text-tertiary");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<RuleTypeIcon>(p => p
            .Add(x => x.RuleType, RuleType.Spam)
            .Add(x => x.Class, "extra")
            .AddUnmatched("data-testid", "rule-icon"));

        var svg = cut.Find("svg");
        svg.ClassList.Should().Contain("extra");
        svg.GetAttribute("data-testid").Should().Be("rule-icon");
    }
}
