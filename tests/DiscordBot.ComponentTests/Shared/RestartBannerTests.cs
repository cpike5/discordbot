using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class RestartBannerTests : TestContext
{
    [Fact]
    public void RendersBanner_ForAuthorizedAdmin()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin");
        auth.SetPolicies("RequireAdmin");

        var cut = RenderComponent<RestartBanner>();

        cut.Markup.Should().Contain("Restart Required");
        cut.Find("a").GetAttribute("href").Should().Be("/Admin/Settings");
    }

    [Fact]
    public void RendersNothing_ForUnauthorizedUser()
    {
        var auth = this.AddTestAuthorization();
        auth.SetNotAuthorized();

        var cut = RenderComponent<RestartBanner>();

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void LinkClick_RaisesOnGoToBotControl_AndHonorsSettingsUrl()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin");
        auth.SetPolicies("RequireAdmin");

        var clicked = false;
        var cut = RenderComponent<RestartBanner>(p => p
            .Add(x => x.SettingsUrl, "/Custom/Settings")
            .Add(x => x.OnGoToBotControl, () => clicked = true));

        var link = cut.Find("a");
        link.GetAttribute("href").Should().Be("/Custom/Settings");
        link.Click();

        clicked.Should().BeTrue();
    }
}
