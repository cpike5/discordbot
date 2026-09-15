using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Layout;

/// <summary>
/// <see cref="MainSidebar"/> ports Pages/Shared/_Sidebar.cshtml's role-gated links one for one
/// (see the component's own header comment). bUnit's fake authorization resolves
/// <c>&lt;AuthorizeView Policy="..."&gt;</c> only against policy names explicitly listed via
/// <c>SetPolicies(...)</c> - plain <c>SetRoles(...)</c> is not enough - so every authorized
/// scenario below lists exactly the named policies that role satisfies, mirroring
/// IdentityServiceExtensions' hierarchical RequireRole lists.
/// </summary>
public class MainSidebarTests : BlazorComponentTestContext
{
    public MainSidebarTests()
    {
        Services.AddSingleton(Mock.Of<IVersionService>(v => v.GetVersion() == "v1.2.3"));
        Services.AddSingleton(Options.Create(new ObservabilityOptions()));
    }

    private void NavigateTo(string path)
        => ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    private BunitAuthorizationContext AuthorizeAs(string role, params string[] policies)
        => AddAuthorization().SetAuthorized($"{role.ToLowerInvariant()}-user").SetRoles(role).SetPolicies(policies);

    [Fact]
    public void Anonymous_SeesOnlyDashboard()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<MainSidebar>();

        cut.Markup.Should().Contain("Dashboard");
        cut.Markup.Should().NotContain("Servers");
        cut.Markup.Should().NotContain(">Commands<");
        cut.Markup.Should().NotContain("Administration");
        cut.Markup.Should().NotContain("Developer");
    }

    [Fact]
    public void Viewer_SeesCommandsAndPerformance_ButNotServersOrAdmin()
    {
        AuthorizeAs("Viewer", "RequireViewer");

        var cut = Render<MainSidebar>();

        cut.Markup.Should().Contain(">Commands<");
        cut.Markup.Should().Contain("Bot Performance");
        cut.Markup.Should().NotContain("Servers");
        cut.Markup.Should().NotContain("Rat Watch Analytics");
        cut.Markup.Should().NotContain("Administration");
    }

    [Fact]
    public void Moderator_SeesServers_ButNotAdminSection()
    {
        AuthorizeAs("Moderator", "RequireModerator", "RequireViewer");

        var cut = Render<MainSidebar>();

        cut.Markup.Should().Contain("Servers");
        cut.Markup.Should().Contain(">Commands<");
        cut.Markup.Should().NotContain("Rat Watch Analytics");
        cut.Markup.Should().NotContain("Administration");
        cut.Markup.Should().NotContain("Developer");
    }

    [Fact]
    public void Admin_SeesAdminAndDeveloperSections_ButNotSuperAdminLinks()
    {
        AuthorizeAs("Admin", "RequireAdmin", "RequireModerator", "RequireViewer");

        var cut = Render<MainSidebar>();

        cut.Markup.Should().Contain("Rat Watch Analytics");
        cut.Markup.Should().Contain("Administration");
        cut.Markup.Should().Contain(">Users<");
        cut.Markup.Should().Contain("Developer");
        cut.Markup.Should().Contain(">Components<");
        cut.Markup.Should().NotContain(">Currency<");
        cut.Markup.Should().NotContain("Bulk Purge");
        cut.Markup.Should().NotContain("User Purge");
    }

    [Fact]
    public void SuperAdmin_SeesEverythingIncludingSuperAdminOnlyLinks()
    {
        AuthorizeAs("SuperAdmin", "RequireSuperAdmin", "RequireAdmin", "RequireModerator", "RequireViewer");

        var cut = Render<MainSidebar>();

        cut.Markup.Should().Contain(">Currency<");
        cut.Markup.Should().Contain("Bulk Purge");
        cut.Markup.Should().Contain("User Purge");
    }

    [Fact]
    public void ActiveLink_GetsActiveClassAndAriaCurrent()
    {
        AuthorizeAs("SuperAdmin", "RequireSuperAdmin", "RequireAdmin", "RequireModerator", "RequireViewer");
        NavigateTo("/Admin/Settings");

        var cut = Render<MainSidebar>();

        var settingsLink = cut.Find("a[title='Settings']");
        settingsLink.ClassList.Should().Contain("active");
        settingsLink.GetAttribute("aria-current").Should().Be("page");

        var usersLink = cut.Find("a[title='Users']");
        usersLink.ClassList.Should().NotContain("active");
        usersLink.HasAttribute("aria-current").Should().BeFalse();
    }

    [Fact]
    public void KibanaAndSeqLinks_HiddenByDefault()
    {
        AuthorizeAs("Admin", "RequireAdmin", "RequireModerator", "RequireViewer");

        var cut = Render<MainSidebar>();

        cut.Markup.Should().NotContain("Kibana");
        cut.Markup.Should().NotContain(">Seq<");
    }

    [Fact]
    public void KibanaAndSeqLinks_ShownWhenConfigured_AsExternalLinks()
    {
        Services.RemoveAll<IOptions<ObservabilityOptions>>();
        Services.AddSingleton(Options.Create(new ObservabilityOptions
        {
            KibanaUrl = "https://kibana.example.test",
            SeqUrl = "https://seq.example.test"
        }));
        AuthorizeAs("Admin", "RequireAdmin", "RequireModerator", "RequireViewer");

        var cut = Render<MainSidebar>();

        var kibanaLink = cut.Find("a[title='Kibana']");
        kibanaLink.GetAttribute("href").Should().Be("https://kibana.example.test");
        kibanaLink.GetAttribute("target").Should().Be("_blank");
        kibanaLink.GetAttribute("rel").Should().Be("noopener noreferrer");

        var seqLink = cut.Find("a[title='Seq']");
        seqLink.GetAttribute("href").Should().Be("https://seq.example.test");
    }

    [Fact]
    public void BotStatusFooter_RendersVersionFromVersionService()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<MainSidebar>();

        cut.Find(".bot-status-version").TextContent.Should().Be("v1.2.3");
    }
}
