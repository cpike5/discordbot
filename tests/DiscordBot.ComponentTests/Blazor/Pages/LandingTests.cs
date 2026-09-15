using Bunit;
using DiscordBot.Bot.Blazor.Pages;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.ComponentTests.Blazor.Pages;

/// <summary>
/// Covers the static SSR port of Pages/Landing.cshtml + LandingModel
/// (docs/plans/blazor-port-plan.md §5 Phase 3) - every section renders, and the footer version
/// comes from the injected <see cref="ApplicationOptions"/> the same way LandingModel.Version did.
/// </summary>
public class LandingTests : BlazorComponentTestContext
{
    public LandingTests()
    {
        Services.AddSingleton<IOptions<ApplicationOptions>>(
            Options.Create(new ApplicationOptions { Version = "9.9.9-test" }));
    }

    [Fact]
    public void RendersHeroAndFooterVersion()
    {
        var cut = Render<Landing>();

        cut.Find("h1").TextContent.Trim().Should().Be("Discord Bot");
        cut.Markup.Should().Contain("v9.9.9-test");
    }

    [Fact]
    public void RendersAllMarketingSections()
    {
        var cut = Render<Landing>();

        cut.Find("section#features").Should().NotBeNull();
        cut.Find("section#tech-stack").Should().NotBeNull();
        cut.Find("section#open-source").Should().NotBeNull();
        cut.Find("section#about").Should().NotBeNull();
    }

    [Fact]
    public void RendersOpenRouterTechStackTile()
    {
        var cut = Render<Landing>();

        cut.Markup.Should().Contain("OpenRouter");
    }

    [Fact]
    public void RendersStickyNavWithFourAnchorLinks()
    {
        var cut = Render<Landing>();

        var nav = cut.Find("nav[data-landing-page]");
        nav.QuerySelectorAll("a.nav-link").Should().HaveCount(4);
    }

    [Fact]
    public void RendersLoginLinkToAccountLogin()
    {
        var cut = Render<Landing>();

        cut.FindAll("a").Should().ContainSingle(a => a.GetAttribute("href") == "/Account/Login" && a.TextContent.Trim() == "Login");
    }

    [Fact]
    public void RendersCopyrightYear()
    {
        var cut = Render<Landing>();

        cut.Find("footer").TextContent.Should().Contain($"{DateTime.UtcNow.Year} cpike");
    }
}
