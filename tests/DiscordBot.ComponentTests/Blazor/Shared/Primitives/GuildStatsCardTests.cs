using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class GuildStatsCardTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersTotalActiveAndInactiveCounts()
    {
        var cut = Render<GuildStatsCard>(p => p
            .Add(x => x.TotalGuilds, 12).Add(x => x.ActiveGuilds, 9).Add(x => x.InactiveGuilds, 3));

        cut.Find("p.text-3xl").TextContent.Should().Be("12");
        cut.Markup.Should().Contain("9 Active");
        cut.Markup.Should().Contain("3 Inactive");
    }

    [Fact]
    public void ZeroInactive_DoesNotRenderInactiveRow()
    {
        var cut = Render<GuildStatsCard>(p => p
            .Add(x => x.TotalGuilds, 5).Add(x => x.ActiveGuilds, 5).Add(x => x.InactiveGuilds, 0));

        cut.Markup.Should().NotContain("Inactive");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<GuildStatsCard>(p => p.Add(x => x.Class, "extra").AddUnmatched("data-testid", "stats"));

        var root = cut.Find("div");
        root.ClassList.Should().Contain("extra");
        root.GetAttribute("data-testid").Should().Be("stats");
    }
}
