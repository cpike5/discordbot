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
}
