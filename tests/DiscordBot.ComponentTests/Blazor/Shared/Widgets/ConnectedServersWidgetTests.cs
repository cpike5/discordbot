using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Components.Enums;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class ConnectedServersWidgetTests : BlazorComponentTestContext
{
    private static readonly ConnectedServerItemViewModel Server = new()
    {
        Id = 555, Name = "Test Guild", Initials = "TG", AvatarGradient = "from-a to-b",
        MemberCount = 42, Status = ServerConnectionStatus.Online, CommandsToday = 3, DetailUrl = "/guilds/555"
    };

    // bUnit's fake authorization only satisfies a named Policy via SetPolicies (SetRoles alone,
    // which is what the base class's AddAuthorizedAdmin() helper sets, does not) - the whole
    // widget is wrapped in <AuthorizeView Policy="RequireModerator">, so every "authorized" test
    // needs this rather than the base helper.
    private void AddAuthorizedModerator() =>
        AddAuthorization().SetAuthorized("admin").SetRoles("Admin").SetPolicies("RequireModerator");

    [Fact]
    public void NotAuthorized_RendersNothing()
    {
        AddAuthorization().SetNotAuthorized();
        var cut = Render<ConnectedServersWidget>(p => p.Add(x => x.Servers, new List<ConnectedServerItemViewModel> { Server }));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void Authorized_NoServers_RendersEmptyState()
    {
        AddAuthorizedModerator();
        var cut = Render<ConnectedServersWidget>();

        cut.Markup.Should().Contain("No connected servers");
    }

    [Fact]
    public void Authorized_RendersServerRow()
    {
        AddAuthorizedModerator();
        var cut = Render<ConnectedServersWidget>(p => p.Add(x => x.Servers, new List<ConnectedServerItemViewModel> { Server }));

        cut.Markup.Should().Contain("Test Guild");
        cut.Markup.Should().Contain("42");
    }

    [Fact]
    public void ToggleMenu_ShowsAndHidesActionDropdown()
    {
        AddAuthorizedModerator();
        var cut = Render<ConnectedServersWidget>(p => p.Add(x => x.Servers, new List<ConnectedServerItemViewModel> { Server }));

        cut.FindAll(".server-action-dropdown").Should().BeEmpty();

        cut.Find("button.server-action-btn").Click();
        cut.FindAll(".server-action-dropdown").Should().ContainSingle();

        cut.Find("button.server-action-btn").Click();
        cut.FindAll(".server-action-dropdown").Should().BeEmpty();
    }

    [Fact]
    public void CopyServerId_CallsBrowserInterop_ClipboardWrite()
    {
        AddAuthorizedModerator();
        var moduleInterop = JSInterop.SetupModule("./js/blazor/browser.js");
        moduleInterop.Setup<bool>("copyToClipboard", _ => true).SetResult(true);

        var cut = Render<ConnectedServersWidget>(p => p.Add(x => x.Servers, new List<ConnectedServerItemViewModel> { Server }));
        cut.Find("button.server-action-btn").Click();

        cut.Find("button[role='menuitem']:last-child").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Copied!"));
    }

    [Fact]
    public void ShowViewAll_OnlyWhenTotalExceedsShown()
    {
        AddAuthorizedModerator();
        var cut = Render<ConnectedServersWidget>(p => p
            .Add(x => x.Servers, new List<ConnectedServerItemViewModel> { Server })
            .Add(x => x.TotalServerCount, 1));
        cut.Markup.Should().NotContain("View All");

        cut = Render<ConnectedServersWidget>(p => p
            .Add(x => x.Servers, new List<ConnectedServerItemViewModel> { Server })
            .Add(x => x.TotalServerCount, 5));
        cut.Markup.Should().Contain("View All");
    }
}
