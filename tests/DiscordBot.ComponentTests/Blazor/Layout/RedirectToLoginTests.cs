using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.ComponentTests.Blazor.Layout;

/// <summary>
/// <see cref="RedirectToLogin"/> is what <c>Routes.razor</c>'s <c>&lt;NotAuthorized&gt;</c>
/// renders for an unauthenticated visitor - see "Auth in components" in
/// <c>docs/architecture/patterns.md</c>. It forces a full page load (<c>forceLoad: true</c>)
/// rather than a client-side navigation, which bUnit's fake <c>NavigationManager</c> records
/// rather than performing, so the assertions here read that record instead of following a real
/// redirect.
/// </summary>
public class RedirectToLoginTests : BlazorComponentTestContext
{
    [Fact]
    public void NavigatesToLogin_WithReturnUrl_AsAForceLoad()
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/admin/blazor-probe");

        Render<RedirectToLogin>();

        var entry = navMan.History.First();
        entry.Uri.Should().Be("/Account/Login?returnUrl=%2Fadmin%2Fblazor-probe");
        entry.Options.ForceLoad.Should().BeTrue();
        entry.State.Should().Be(NavigationState.Succeeded);
    }

    [Fact]
    public void NavigatesToLogin_WithProtocolRelativeReturnUrl_FallsBackToRoot()
    {
        // A base-relative path starting with "/" would make the "/" + relativePath return URL
        // start with "//", a protocol-relative URL a browser would treat as an off-site
        // redirect. Reject it and fall back to "/" instead of handing it to Login.
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("http://localhost//evil.example.com/phish");

        Render<RedirectToLogin>();

        var entry = navMan.History.First();
        entry.Uri.Should().Be("/Account/Login?returnUrl=%2F");
    }

    [Fact]
    public void NavigatesToLogin_WithReturnUrlContainingScheme_FallsBackToRoot()
    {
        // A relative path that embeds "scheme://" anywhere is also a potential off-site
        // redirect once handed to Login's LocalRedirect; reject it too.
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("http://localhost/redirect/http://evil.example.com");

        Render<RedirectToLogin>();

        var entry = navMan.History.First();
        entry.Uri.Should().Be("/Account/Login?returnUrl=%2F");
    }
}
