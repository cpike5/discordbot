using Bunit;
using DiscordBot.Bot.Blazor.Pages.Error;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Pages.Error;

/// <summary>
/// Covers the static SSR port of Pages/Error/403.cshtml + ForbiddenModel
/// (docs/plans/blazor-port-plan.md §5 Phase 3). The authenticated/anonymous branch that used to
/// read User.Identity.IsAuthenticated off the PageModel's ambient ClaimsPrincipal now reads the
/// cascading Task&lt;AuthenticationState&gt; bUnit's <c>AddAuthorization()</c> supplies.
/// </summary>
public class ForbiddenTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersHeadingAndMessage()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<Forbidden>();

        cut.Find("h1").TextContent.Should().Be("Access Forbidden");
        cut.Markup.Should().Contain("403 Forbidden");
    }

    [Fact]
    public void Authenticated_ShowsDashboardAndGoBack_NotSignIn()
    {
        AddAuthorization().SetAuthorized("admin").SetRoles("Admin");

        var cut = Render<Forbidden>();

        cut.FindAll("a").Should().ContainSingle(a => a.TextContent.Trim() == "Go to Dashboard" && a.GetAttribute("href") == "/");
        cut.FindAll("button[data-error-action='back']").Should().ContainSingle(b => b.TextContent.Trim() == "Go Back");
        cut.FindAll("a").Should().NotContain(a => a.TextContent.Trim() == "Sign In");
        cut.Markup.Should().Contain("currently signed in");
    }

    [Fact]
    public void Anonymous_ShowsDashboardAndSignIn_NotGoBack()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<Forbidden>();

        cut.FindAll("a").Should().ContainSingle(a => a.TextContent.Trim() == "Go to Dashboard" && a.GetAttribute("href") == "/");
        cut.FindAll("a").Should().ContainSingle(a => a.TextContent.Trim() == "Sign In" && a.GetAttribute("href") == "/Account/Login");
        cut.FindAll("button[data-error-action='back']").Should().BeEmpty();
        cut.Markup.Should().Contain("need to be signed in");
    }
}
