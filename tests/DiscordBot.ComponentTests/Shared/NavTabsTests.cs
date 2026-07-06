using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Shared;

public class NavTabsTests : TestContext
{
    private static readonly IReadOnlyList<NavTabItem> Tabs =
    [
        new NavTabItem { Id = "overview", Label = "Overview" },
        new NavTabItem { Id = "settings", Label = "Settings" },
    ];

    private IRenderedComponent<NavTabs> Render(Action<ComponentParameterCollectionBuilder<NavTabs>>? extra = null) =>
        RenderComponent<NavTabs>(p =>
        {
            p.Add(x => x.Tabs, Tabs)
             .Add(x => x.ContainerId, "myTabs")
             .Add(x => x.TabContent, (string tabId) => (RenderFragment)(b => b.AddContent(0, $"panel:{tabId}")));
            extra?.Invoke(p);
        });

    [Fact]
    public void FirstTabActive_ByDefault_AndInactivePanelHidden()
    {
        var cut = Render();

        cut.Find("#myTabs-tab-overview").ClassList.Should().Contain("active");
        cut.Find("#myTabs-tab-overview").GetAttribute("aria-selected").Should().Be("true");
        cut.Find("#myTabs-panel-overview").HasAttribute("hidden").Should().BeFalse();
        cut.Find("#myTabs-panel-settings").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public void Click_SwitchesTab_AndRaisesActiveTabChanged()
    {
        string? changedTo = null;
        var cut = Render(p => p.Add(x => x.ActiveTabChanged, (string id) => changedTo = id));

        cut.Find("#myTabs-tab-settings").Click();

        changedTo.Should().Be("settings");
        cut.Instance.ActiveTab.Should().Be("settings");
        cut.Find("#myTabs-tab-settings").ClassList.Should().Contain("active");
        cut.Find("#myTabs-panel-settings").HasAttribute("hidden").Should().BeFalse();
        cut.Find("#myTabs-panel-overview").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public void DisabledTab_DoesNotSwitch()
    {
        var tabs = new List<NavTabItem>
        {
            new() { Id = "one", Label = "One" },
            new() { Id = "two", Label = "Two", Disabled = true },
        };
        var cut = RenderComponent<NavTabs>(p => p.Add(x => x.Tabs, tabs));

        cut.Find("[data-tab-id=two]").HasAttribute("disabled").Should().BeTrue();
        cut.Instance.ActiveTab.Should().Be("one");
    }

    [Fact]
    public void PageNavigationMode_RendersAnchors_WithHrefs()
    {
        var tabs = new List<NavTabItem>
        {
            new() { Id = "a", Label = "A", Href = "/guilds/1/a" },
            new() { Id = "b", Label = "B", Href = "/guilds/1/b" },
        };
        var cut = RenderComponent<NavTabs>(p => p
            .Add(x => x.Tabs, tabs)
            .Add(x => x.NavigationMode, NavMode.PageNavigation));

        var anchors = cut.FindAll("a[role=tab]");
        anchors.Should().HaveCount(2);
        anchors.Select(a => a.GetAttribute("href")).Should().Equal("/guilds/1/a", "/guilds/1/b");
    }
}
