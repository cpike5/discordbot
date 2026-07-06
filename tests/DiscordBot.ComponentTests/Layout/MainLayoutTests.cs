using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.ComponentTests.Layout;

public class MainLayoutTests : TestContext
{
    private static readonly RenderFragment BodyContent =
        builder => builder.AddMarkupContent(0, "<p id=\"test-body\">PAGE-BODY-MARKER</p>");

    private static readonly RenderFragment BellStub =
        builder => builder.AddMarkupContent(0, "<div id=\"bell-stub\">BELL-STUB</div>");

    public MainLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var version = new Mock<IVersionService>();
        version.Setup(v => v.GetVersion()).Returns("v9.9.9-test");
        Services.AddSingleton(version.Object);
        Services.AddSingleton(Options.Create(new ObservabilityOptions()));

        // The logout form contains <AntiforgeryToken />; a null token renders nothing.
        Services.AddSingleton<AntiforgeryStateProvider>(new FakeAntiforgeryStateProvider());
    }

    private IRenderedComponent<MainLayout> RenderLayout() =>
        RenderComponent<MainLayout>(p => p
            .Add(l => l.Body, BodyContent)
            .Add(l => l.BellContent, BellStub));

    [Fact]
    public void RendersNavbarBrandBodyAndSidebar_ForAuthorizedAdmin()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("Admin User");
        auth.SetPolicies("RequireAdmin", "RequireModerator", "RequireViewer", "RequireSuperAdmin");

        var cut = RenderLayout();

        // Navbar brand + chrome
        cut.Markup.Should().Contain("Bot Admin");
        cut.Find("nav.navbar-redesign").Should().NotBeNull();

        // @Body renders inside the main content area
        cut.Find("main#main-content #test-body").TextContent.Should().Be("PAGE-BODY-MARKER");

        // Bell area rendered via the overridable BellContent parameter
        cut.Find("#bell-stub").TextContent.Should().Be("BELL-STUB");

        // Sidebar with auth-gated links + version footer
        cut.Find("aside#sidebar").Should().NotBeNull();
        cut.Markup.Should().Contain("Servers");
        cut.Markup.Should().Contain("Bulk Purge");
        cut.Markup.Should().Contain("v9.9.9-test");

        // Signed-in navbar: user menu with display name, no Sign In button
        cut.Find("#userMenuButton").Should().NotBeNull();
        cut.Markup.Should().Contain("Admin User");
        cut.Markup.Should().Contain("Sign out");
        cut.Markup.Should().NotContain("Sign In");
    }

    [Fact]
    public void RendersSignInAndHidesGatedNav_ForAnonymousUser()
    {
        var auth = this.AddTestAuthorization();
        auth.SetNotAuthorized();

        var cut = RenderLayout();

        // Body + brand still render
        cut.Find("#test-body").TextContent.Should().Be("PAGE-BODY-MARKER");
        cut.Markup.Should().Contain("Bot Admin");

        // Anonymous navbar: Sign In link, no user menu
        cut.Find("a[href='/Account/Login']").TextContent.Trim().Should().Be("Sign In");
        cut.FindAll("#userMenuButton").Should().BeEmpty();

        // Policy-gated sidebar sections are absent (Dashboard link is unguarded)
        cut.Markup.Should().Contain("Dashboard");
        cut.Markup.Should().NotContain("Bulk Purge");
        cut.Markup.Should().NotContain("Administration");
    }

    [Fact]
    public void HidesModeratorNav_ForViewerOnlyUser()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("Viewer User");
        auth.SetPolicies("RequireViewer");

        var cut = RenderLayout();

        cut.Markup.Should().Contain("Commands");
        cut.Markup.Should().Contain("Bot Performance");
        cut.Markup.Should().NotContain("Servers");
        cut.Markup.Should().NotContain("Administration");
    }

    private sealed class FakeAntiforgeryStateProvider : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken? GetAntiforgeryToken() => null;
    }
}
