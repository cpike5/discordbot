using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class GuildContextSelectorTests : TestContext
{
    private const string Route = "/Guilds/{guildId}/Members";

    private static GuildSelectorItem Guild(string id, string name) =>
        new() { GuildId = id, GuildName = name };

    [Fact]
    public void RendersPlaceholder_WhenNoGuilds()
    {
        var cut = RenderComponent<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, Route)
            .Add(x => x.Guilds, new List<GuildSelectorItem>()));

        cut.Markup.Should().Contain("No guilds available");
        cut.FindAll("a").Should().BeEmpty();
    }

    [Fact]
    public void RendersDirectLink_WhenSingleGuild()
    {
        var cut = RenderComponent<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, Route)
            .Add(x => x.Guilds, new List<GuildSelectorItem> { Guild("123456789012345678", "Gaming") }));

        var link = cut.Find("a");
        link.GetAttribute("href").Should().Be("/Guilds/123456789012345678/Members");
        link.TextContent.Should().Contain("Open in Gaming");
    }

    [Fact]
    public void DropdownTogglesOpen_AndListsGuildLinks()
    {
        var cut = RenderComponent<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, Route)
            .Add(x => x.Guilds, new List<GuildSelectorItem>
            {
                Guild("111111111111111111", "Alpha"),
                Guild("222222222222222222", "Beta"),
            }));

        cut.FindAll("a").Should().BeEmpty("dropdown starts closed");

        cut.Find("button").Click();

        cut.Find("button").GetAttribute("aria-expanded").Should().Be("true");
        var hrefs = cut.FindAll("a").Select(a => a.GetAttribute("href")).ToList();
        hrefs.Should().Equal("/Guilds/111111111111111111/Members", "/Guilds/222222222222222222/Members");
    }

    [Fact]
    public void BackdropClick_ClosesDropdown()
    {
        var cut = RenderComponent<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, Route)
            .Add(x => x.Guilds, new List<GuildSelectorItem>
            {
                Guild("111111111111111111", "Alpha"),
                Guild("222222222222222222", "Beta"),
            }));

        cut.Find("button").Click();
        cut.Find("div.fixed.inset-0").Click();

        cut.FindAll("a").Should().BeEmpty();
        cut.Find("button").GetAttribute("aria-expanded").Should().Be("false");
    }
}
