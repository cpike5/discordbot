using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

/// <summary>
/// Component tests for <see cref="StatTile"/> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster
/// 4d) - the "label over big number" tile pulled out of the Guild Details dashboard widgets.
/// </summary>
public class StatTileTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersLabelAndValue()
    {
        var cut = Render<StatTile>(p => p.Add(x => x.Label, "Total").Add(x => x.Value, "12"));

        cut.Find("p.text-xs").TextContent.Should().Be("Total");
        cut.Find("p.text-2xl").TextContent.Should().Be("12");
    }

    [Fact]
    public void DefaultValueClass_IsTextPrimary()
    {
        var cut = Render<StatTile>(p => p.Add(x => x.Label, "Total").Add(x => x.Value, "12"));

        cut.Find("p.text-2xl").ClassList.Should().Contain("text-text-primary");
    }

    [Fact]
    public void CustomValueClass_OverridesDefault()
    {
        var cut = Render<StatTile>(p => p
            .Add(x => x.Label, "Failed")
            .Add(x => x.Value, "3")
            .Add(x => x.ValueClass, "text-error"));

        var valueEl = cut.Find("p.text-2xl");
        valueEl.ClassList.Should().Contain("text-error");
        valueEl.ClassList.Should().NotContain("text-text-primary");
    }

    [Fact]
    public void Class_IsPassedThrough()
    {
        var cut = Render<StatTile>(p => p
            .Add(x => x.Label, "Total")
            .Add(x => x.Value, "12")
            .Add(x => x.Class, "extra"));

        cut.Find("div").ClassList.Should().Contain("extra");
    }
}
