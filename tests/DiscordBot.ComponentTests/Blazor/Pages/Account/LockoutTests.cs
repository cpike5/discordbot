using Bunit;
using DiscordBot.Bot.Blazor.Pages.Account;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Pages.Account;

/// <summary>
/// Covers the static SSR port of Pages/Account/Lockout.cshtml + LockoutModel
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). Static copy, [AllowAnonymous] - no auth
/// setup needed, matches ForbiddenTests/AccessDeniedTests for the other anonymous-reachable pages.
/// </summary>
public class LockoutTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersHeadingAndMessage()
    {
        var cut = Render<Lockout>();

        cut.Find("h1").TextContent.Should().Be("Account Locked");
        cut.Markup.Should().Contain("temporarily locked");
    }

    [Fact]
    public void RendersReturnToLoginLink()
    {
        var cut = Render<Lockout>();

        cut.FindAll("a").Should().ContainSingle(a => a.TextContent.Trim() == "Return to Login" && a.GetAttribute("href") == "/Account/Login");
    }
}
