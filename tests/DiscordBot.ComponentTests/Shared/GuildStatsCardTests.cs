using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class GuildStatsCardTests : TestContext
{
    [Fact]
    public void RendersTotalAndActiveCounts()
    {
        var cut = RenderComponent<GuildStatsCard>(p => p
            .Add(x => x.TotalGuilds, 12)
            .Add(x => x.ActiveGuilds, 10)
            .Add(x => x.InactiveGuilds, 2));

        cut.Find("p.text-3xl").TextContent.Should().Be("12");
        cut.Markup.Should().Contain("10 Active");
        cut.Markup.Should().Contain("2 Inactive");
    }

    [Fact]
    public void HidesInactiveLabel_WhenNoInactiveGuilds()
    {
        var cut = RenderComponent<GuildStatsCard>(p => p
            .Add(x => x.TotalGuilds, 5)
            .Add(x => x.ActiveGuilds, 5)
            .Add(x => x.InactiveGuilds, 0));

        cut.Markup.Should().NotContain("Inactive");
        cut.Markup.Should().Contain("5 Active");
    }
}
