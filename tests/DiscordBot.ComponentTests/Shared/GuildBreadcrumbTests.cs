using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class GuildBreadcrumbTests : TestContext
{
    [Fact]
    public void RendersNothing_WhenNoItems()
    {
        var cut = RenderComponent<GuildBreadcrumb>();

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void RendersLinks_ForNonCurrentItems_AndPlainTextForCurrent()
    {
        var cut = RenderComponent<GuildBreadcrumb>(p => p
            .Add(x => x.Items, new List<BreadcrumbItem>
            {
                new() { Label = "Guilds", Url = "/guilds" },
                new() { Label = "My Guild", IsCurrent = true },
            }));

        var link = cut.Find("a");
        link.GetAttribute("href").Should().Be("/guilds");
        link.TextContent.Trim().Should().Be("Guilds");
        cut.Find("span.text-text-primary").TextContent.Trim().Should().Be("My Guild");
        cut.FindAll("svg").Should().HaveCount(1); // separator only after non-current items
    }
}
