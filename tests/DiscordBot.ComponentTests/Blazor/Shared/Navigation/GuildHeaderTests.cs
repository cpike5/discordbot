using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Navigation;

public class GuildHeaderTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersPageTitleAndDescription()
    {
        var cut = Render<GuildHeader>(p => p
            .Add(x => x.GuildId, "123456789012345678")
            .Add(x => x.Name, "My Server")
            .Add(x => x.PageTitle, "Server Settings")
            .Add(x => x.PageDescription, "Configure the bot."));

        cut.Find("h1").TextContent.Should().Be("Server Settings");
        cut.Markup.Should().Contain("Configure the bot.");
    }

    [Fact]
    public void NoIconUrl_RendersInitialsFallback()
    {
        var cut = Render<GuildHeader>(p => p.Add(x => x.Name, "Awesome Server").Add(x => x.PageTitle, "x"));
        cut.FindAll("img").Should().BeEmpty();
        cut.Markup.Should().Contain("AW");
    }

    [Fact]
    public void IconUrl_RendersImg()
    {
        var cut = Render<GuildHeader>(p => p.Add(x => x.Name, "Server").Add(x => x.IconUrl, "https://cdn/x.png").Add(x => x.PageTitle, "x"));
        var img = cut.Find("img");
        img.GetAttribute("src").Should().Be("https://cdn/x.png");
        img.GetAttribute("alt").Should().Be("Server");
    }

    [Fact]
    public void StatusBadge_RendersThroughBadgeComponent()
    {
        var cut = Render<GuildHeader>(p => p
            .Add(x => x.Name, "Server").Add(x => x.PageTitle, "x")
            .Add(x => x.StatusBadge, new BadgeViewModel { Text = "Active", Variant = BadgeVariant.Success }));

        var badge = cut.Find("span.badge");
        badge.ClassList.Should().Contain("badge-success");
        badge.TextContent.Should().Contain("Active");
    }

    [Fact]
    public void NoStatusBadgeOrActions_RendersNoRightColumn()
    {
        var cut = Render<GuildHeader>(p => p.Add(x => x.Name, "Server").Add(x => x.PageTitle, "x"));
        cut.FindAll("a").Should().BeEmpty();
        cut.FindAll("span.badge").Should().BeEmpty();
    }

    [Theory]
    [InlineData(HeaderActionStyle.Primary, "bg-accent-orange")]
    [InlineData(HeaderActionStyle.Secondary, "bg-bg-secondary")]
    [InlineData(HeaderActionStyle.Link, "text-accent-blue")]
    public void Actions_RenderExpectedStyleClass(HeaderActionStyle style, string expectedClass)
    {
        var cut = Render<GuildHeader>(p => p
            .Add(x => x.Name, "Server").Add(x => x.PageTitle, "x")
            .Add(x => x.Actions, new List<HeaderAction> { new() { Label = "Go", Url = "/go", Style = style } }));

        var link = cut.Find("a[href='/go']");
        link.ClassList.Should().Contain(expectedClass);
        link.TextContent.Should().Contain("Go");
    }

    [Fact]
    public void ActionWithIcon_RendersThroughIconComponent()
    {
        var cut = Render<GuildHeader>(p => p
            .Add(x => x.Name, "Server").Add(x => x.PageTitle, "x")
            .Add(x => x.Actions, new List<HeaderAction> { new() { Label = "Save", Url = "#", Icon = IconPaths.CheckCircle } }));

        cut.Find("a svg path").GetAttribute("d").Should().Be(IconPaths.CheckCircle);
    }

    [Fact]
    public void OpenInNewTab_SetsTargetAndRel()
    {
        var cut = Render<GuildHeader>(p => p
            .Add(x => x.Name, "Server").Add(x => x.PageTitle, "x")
            .Add(x => x.Actions, new List<HeaderAction> { new() { Label = "Discord", Url = "#", OpenInNewTab = true } }));

        var link = cut.Find("a");
        link.GetAttribute("target").Should().Be("_blank");
        link.GetAttribute("rel").Should().Be("noopener noreferrer");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<GuildHeader>(p => p
            .Add(x => x.Name, "Server").Add(x => x.PageTitle, "x")
            .Add(x => x.Class, "extra")
            .AddUnmatched("data-testid", "guild-header"));

        var root = cut.Find("div");
        root.ClassList.Should().Contain("extra");
        root.GetAttribute("data-testid").Should().Be("guild-header");
    }
}
