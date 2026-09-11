using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class RestartBannerTests : BlazorComponentTestContext
{
    [Fact]
    public void NotAuthorized_RendersNothing()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<RestartBanner>();

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void AuthorizedNonAdmin_RendersNothing()
    {
        AddAuthorization().SetAuthorized("member").SetRoles("Member");

        var cut = Render<RestartBanner>();

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void Admin_RendersBanner_WithGoToBotControlLink()
    {
        // bUnit's fake authorization fails closed on a named Policy= it has never heard of - the
        // shared AddAuthorizedAdmin() helper only sets roles/claims, so "RequireAdmin" (a real
        // AuthorizationOptions.AddPolicy registration in the app, not reproduced by this test
        // host) needs to be told explicitly which policies the fake authorized user passes.
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");

        var cut = Render<RestartBanner>();

        cut.Find("h3").TextContent.Should().Be("Restart Required");
        var link = cut.Find("a");
        link.GetAttribute("href").Should().Be("/Admin/Settings");
        link.TextContent.Should().Contain("Go to Bot Control");
    }

    [Fact]
    public void Admin_ClickWithOnOpenBotControl_PreventsNavigation_AndInvokesCallback()
    {
        // bUnit's fake authorization fails closed on a named Policy= it has never heard of - the
        // shared AddAuthorizedAdmin() helper only sets roles/claims, so "RequireAdmin" (a real
        // AuthorizationOptions.AddPolicy registration in the app, not reproduced by this test
        // host) needs to be told explicitly which policies the fake authorized user passes.
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        var opened = false;
        var cut = Render<RestartBanner>(p => p.Add(x => x.OnOpenBotControl, EventCallback.Factory.Create(this, () => opened = true)));

        cut.Find("a").Click();

        opened.Should().BeTrue();
    }

    [Fact]
    public void Admin_Class_And_AdditionalAttributes_ArePassedThrough()
    {
        // bUnit's fake authorization fails closed on a named Policy= it has never heard of - the
        // shared AddAuthorizedAdmin() helper only sets roles/claims, so "RequireAdmin" (a real
        // AuthorizationOptions.AddPolicy registration in the app, not reproduced by this test
        // host) needs to be told explicitly which policies the fake authorized user passes.
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        var cut = Render<RestartBanner>(p => p.Add(x => x.Class, "extra-class").AddUnmatched("data-testid", "my-banner"));

        var root = cut.Find("div.mb-6");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("my-banner");
    }
}
