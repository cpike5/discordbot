using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Navigation;

public class GuildContextSelectorTests : BlazorComponentTestContext
{
    [Fact]
    public void NoGuilds_RendersFallbackText()
    {
        var cut = Render<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, "/Guilds/{guildId}")
            .Add(x => x.Guilds, Array.Empty<GuildSelectorItem>()));

        cut.Markup.Should().Contain("No guilds available");
        cut.FindAll("a").Should().BeEmpty();
    }

    [Fact]
    public void SingleGuild_RendersDirectLink_WithSubstitutedRoute()
    {
        var cut = Render<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, "/Guilds/{guildId}")
            .Add(x => x.Guilds, new[] { new GuildSelectorItem { GuildId = "42", GuildName = "Solo" } }));

        var link = cut.Find("a");
        link.GetAttribute("href").Should().Be("/Guilds/42");
        link.TextContent.Should().Contain("Open in Solo");
    }

    private static readonly GuildSelectorItem[] Multiple =
    [
        new() { GuildId = "1", GuildName = "Alpha" },
        new() { GuildId = "2", GuildName = "Beta" }
    ];

    [Fact]
    public void MultipleGuilds_DropdownStartsClosed_AndListsAllLinks()
    {
        var cut = Render<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, "/Guilds/{guildId}")
            .Add(x => x.Guilds, Multiple));

        cut.Find("div.hidden").Should().NotBeNull();
        var links = cut.FindAll("a").Select(a => a.GetAttribute("href")).ToList();
        links.Should().Contain(["/Guilds/1", "/Guilds/2"]);
    }

    [Fact]
    public void ButtonClick_TogglesDropdownOpen()
    {
        var cut = Render<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, "/Guilds/{guildId}")
            .Add(x => x.Guilds, Multiple));

        cut.Find("button").Click();

        cut.FindAll("div.hidden").Should().BeEmpty();
    }

    [Fact]
    public async Task Escape_ClosesDropdown()
    {
        var cut = Render<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, "/Guilds/{guildId}")
            .Add(x => x.Guilds, Multiple));

        cut.Find("button").Click();
        cut.FindAll("div.hidden").Should().BeEmpty();

        await cut.Find("div.relative div").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.Find("div.hidden").Should().NotBeNull();
    }

    [Fact]
    public async Task Escape_WhileClosed_StaysClosed_InsteadOfToggling()
    {
        // Regression: HandleKeyDown used to call Toggle(), so Escape on an already-closed
        // dropdown would incorrectly open it. It must always close, never toggle.
        var cut = Render<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, "/Guilds/{guildId}")
            .Add(x => x.Guilds, Multiple));

        cut.FindAll("div.hidden").Should().NotBeEmpty();

        await cut.Find("div.relative div").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("div.hidden").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Dispose_SwallowsJSDisconnectedException_FromOffClickOutside()
    {
        var moduleInterop = JSInterop.SetupModule("./js/blazor/browser.js");
        moduleInterop.Setup<int>("onClickOutside", _ => true).SetResult(1);
        moduleInterop.SetupVoid("offClickOutside", _ => true).SetException(new Microsoft.JSInterop.JSDisconnectedException("circuit gone"));

        var cut = Render<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, "/Guilds/{guildId}")
            .Add(x => x.Guilds, Multiple));
        cut.Find("button").Click(); // opens, registering the click-outside handler

        var act = async () => await DisposeComponentsAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<GuildContextSelector>(p => p
            .Add(x => x.RouteTemplate, "/Guilds/{guildId}")
            .Add(x => x.Guilds, Multiple)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "my-selector"));

        var root = cut.Find("div.relative");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("my-selector");
    }
}
