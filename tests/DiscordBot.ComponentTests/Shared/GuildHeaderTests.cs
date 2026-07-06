using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class GuildHeaderTests : TestContext
{
    [Fact]
    public void RendersIconImage_TitleAndDescription()
    {
        var cut = RenderComponent<GuildHeader>(p => p
            .Add(x => x.Model, new GuildHeaderViewModel
            {
                GuildName = "Test Guild",
                GuildIconUrl = "https://cdn.example/icon.png",
                PageTitle = "Settings",
                PageDescription = "Configure your guild",
            }));

        cut.Find("img").GetAttribute("src").Should().Be("https://cdn.example/icon.png");
        cut.Find("h1").TextContent.Trim().Should().Be("Settings");
        cut.Markup.Should().Contain("Configure your guild");
    }

    [Fact]
    public void RendersInitialsFallback_WhenNoIconUrl()
    {
        var cut = RenderComponent<GuildHeader>(p => p
            .Add(x => x.Model, new GuildHeaderViewModel
            {
                GuildName = "cool guild",
                PageTitle = "Overview",
            }));

        cut.FindAll("img").Should().BeEmpty();
        cut.Markup.Should().Contain("CO");
    }

    [Fact]
    public void RendersStatusBadge_AndActionLinks()
    {
        var cut = RenderComponent<GuildHeader>(p => p
            .Add(x => x.Model, new GuildHeaderViewModel
            {
                GuildName = "Test Guild",
                PageTitle = "Settings",
                StatusBadge = new BadgeViewModel { Text = "Active", Variant = BadgeVariant.Success },
                Actions =
                [
                    new HeaderAction { Label = "Open Portal", Url = "/portal", Style = HeaderActionStyle.Primary, OpenInNewTab = true },
                ],
            }));

        cut.Markup.Should().Contain("Active").And.Contain("bg-success");
        var action = cut.Find("a");
        action.GetAttribute("href").Should().Be("/portal");
        action.GetAttribute("target").Should().Be("_blank");
    }
}
