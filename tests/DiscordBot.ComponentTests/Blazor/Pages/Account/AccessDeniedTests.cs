using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Account;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.ComponentTests.Blazor.Pages.Account;

/// <summary>
/// Covers the static SSR port of Pages/Account/AccessDenied.cshtml + AccessDeniedModel
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). [AllowAnonymous], so no auth setup is
/// needed to render it - matches ForbiddenTests' anonymous-reachable pages.
/// <see cref="TestAntiforgeryStateProvider"/> backs the page's plain <c>&lt;form&gt;</c>'s
/// <c>&lt;AntiforgeryToken /&gt;</c> - the same registration <c>MainNavbarTests</c> uses for its
/// own plain-form logout button.
/// </summary>
public class AccessDeniedTests : BlazorComponentTestContext
{
    public AccessDeniedTests()
    {
        Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();
    }

    private IRenderedComponent<AccessDenied> RenderWithReturnUrl(string? returnUrl)
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo(returnUrl is null ? "/Account/AccessDenied" : navMan.GetUriWithQueryParameter("returnUrl", returnUrl));
        return Render<AccessDenied>();
    }

    [Fact]
    public void RendersHeadingAndMessage()
    {
        var cut = RenderWithReturnUrl(null);

        cut.Find("h1").TextContent.Should().Be("Access Denied");
        cut.Markup.Should().Contain("403 Forbidden");
    }

    [Fact]
    public void NoReturnUrl_DoesNotRenderAttemptedUrlLine()
    {
        var cut = RenderWithReturnUrl(null);

        cut.Markup.Should().NotContain("Attempted URL");
    }

    [Fact]
    public void WithReturnUrl_RendersAttemptedUrlLine()
    {
        var cut = RenderWithReturnUrl("/Admin/Settings");

        cut.Markup.Should().Contain("Attempted URL:").And.Contain("/Admin/Settings");
    }

    [Fact]
    public void RendersGoToDashboardLink()
    {
        var cut = RenderWithReturnUrl(null);

        cut.FindAll("a").Should().ContainSingle(a => a.TextContent.Trim() == "Go to Dashboard" && a.GetAttribute("href") == "/");
    }

    [Fact]
    public void SignOutForm_PostsToAccountLogout_WithAntiforgeryToken()
    {
        var cut = RenderWithReturnUrl(null);

        var form = cut.FindAll("form").Should().ContainSingle().Subject;
        form.GetAttribute("method").Should().Be("post");
        form.GetAttribute("action").Should().Be("/Account/Logout");
        form.QuerySelector("input[name='__RequestVerificationToken']").Should().NotBeNull();

        var submit = form.QuerySelector("button[type='submit']");
        submit.Should().NotBeNull();
        submit!.TextContent.Trim().Should().Be("Sign Out");
    }
}
