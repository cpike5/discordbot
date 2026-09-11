using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Navigation;

public class TabGroupTests : BlazorComponentTestContext
{
    private static readonly IReadOnlyList<TabItem> ThreeTabs =
    [
        new() { Id = "a", Label = "Alpha", IconPath = IconPaths.InformationCircle },
        new() { Id = "b", Label = "Beta", BadgeCount = 5, BadgeVariant = TabBadgeVariant.Success, Subtitle = "sub" },
        new() { Id = "c", Label = "Gamma", Disabled = true }
    ];

    [Fact]
    public void InPage_RendersButtonsWithRoleTab_AndOnlyActivePanel()
    {
        var cut = Render<TabGroup>(p => p
            .Add(x => x.Tabs, ThreeTabs)
            .Add(x => x.ActiveTabId, "a")
            .Add(x => x.Mode, TabGroupMode.InPage)
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabPanel>(0);
                builder.AddComponentParameter(1, nameof(TabPanel.Id), "a");
                builder.AddComponentParameter(2, nameof(TabPanel.ChildContent), (RenderFragment)(b => b.AddMarkupContent(0, "<p data-testid='panel-a'>A</p>")));
                builder.CloseComponent();

                builder.OpenComponent<TabPanel>(3);
                builder.AddComponentParameter(4, nameof(TabPanel.Id), "b");
                builder.AddComponentParameter(5, nameof(TabPanel.ChildContent), (RenderFragment)(b => b.AddMarkupContent(0, "<p data-testid='panel-b'>B</p>")));
                builder.CloseComponent();
            })));

        cut.FindAll("button[role='tab']").Should().HaveCount(3);
        cut.Find("[data-testid='panel-a']").Should().NotBeNull();
        cut.FindAll("[data-testid='panel-b']").Should().BeEmpty();
    }

    [Fact]
    public void InPage_ClickingTab_ActivatesIt_AndRaisesActiveTabIdChanged()
    {
        string? changedTo = null;
        var cut = Render<TabGroup>(p => p
            .Add(x => x.Tabs, ThreeTabs)
            .Add(x => x.ActiveTabId, "a")
            .Add(x => x.Mode, TabGroupMode.InPage)
            .Add(x => x.ActiveTabIdChanged, EventCallback.Factory.Create<string?>(this, v => changedTo = v)));

        cut.Find("button[data-tab-id='b']").Click();

        changedTo.Should().Be("b");
    }

    [Fact]
    public void InPage_DisabledTab_CannotBeActivated()
    {
        string? changedTo = "unset";
        var cut = Render<TabGroup>(p => p
            .Add(x => x.Tabs, ThreeTabs)
            .Add(x => x.ActiveTabId, "a")
            .Add(x => x.Mode, TabGroupMode.InPage)
            .Add(x => x.ActiveTabIdChanged, EventCallback.Factory.Create<string?>(this, v => changedTo = v)));

        var disabledButton = cut.Find("button[data-tab-id='c']");
        disabledButton.HasAttribute("disabled").Should().BeTrue();
        disabledButton.Click();

        changedTo.Should().Be("unset");
    }

    [Fact]
    public async Task InPage_ArrowRight_MovesToNextEnabledTab_SkippingDisabled_AndWrapsAround()
    {
        string? changedTo = null;
        var cut = Render<TabGroup>(p => p
            .Add(x => x.Tabs, ThreeTabs)
            .Add(x => x.ActiveTabId, "b")
            .Add(x => x.Mode, TabGroupMode.InPage)
            .Add(x => x.ActiveTabIdChanged, EventCallback.Factory.Create<string?>(this, v => changedTo = v)));

        // b -> c is disabled, so ArrowRight from b should wrap to the only other enabled tab: a.
        await cut.Find("button[data-tab-id='b']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });

        changedTo.Should().Be("a");
    }

    [Fact]
    public async Task InPage_Home_And_End_JumpToFirstAndLastEnabledTab()
    {
        string? changedTo = null;
        var cut = Render<TabGroup>(p => p
            .Add(x => x.Tabs, ThreeTabs)
            .Add(x => x.ActiveTabId, "a")
            .Add(x => x.Mode, TabGroupMode.InPage)
            .Add(x => x.ActiveTabIdChanged, EventCallback.Factory.Create<string?>(this, v => changedTo = v)));

        await cut.Find("button[data-tab-id='a']").KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        // c is disabled, so End should land on b (the last enabled tab).
        changedTo.Should().Be("b");

        await cut.Find("button[data-tab-id='a']").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        changedTo.Should().Be("a");
    }

    [Fact]
    public void Navigation_RendersNavLinks_WithHrefAndAriaSelected()
    {
        var cut = Render<TabGroup>(p => p
            .Add(x => x.Tabs, ThreeTabs)
            .Add(x => x.ActiveTabId, "b")
            .Add(x => x.Mode, TabGroupMode.Navigation));

        cut.FindAll("a[role='tab']").Should().HaveCount(3);
        var activeLink = cut.Find("a[data-tab-id='b']");
        activeLink.GetAttribute("aria-selected").Should().Be("true");
        activeLink.GetAttribute("tabindex").Should().Be("0");

        var inactiveLink = cut.Find("a[data-tab-id='a']");
        inactiveLink.GetAttribute("aria-selected").Should().Be("false");
        inactiveLink.GetAttribute("tabindex").Should().Be("-1");
    }

    [Fact]
    public void RendersBadgeAndSubtitle_WhenSet()
    {
        var cut = Render<TabGroup>(p => p.Add(x => x.Tabs, ThreeTabs).Add(x => x.ActiveTabId, "a"));

        var badge = cut.Find("button[data-tab-id='b'] .tab-badge");
        badge.TextContent.Should().Be("5");
        cut.Find("button[data-tab-id='b'] .tab-subtitle").TextContent.Should().Be("sub");
    }

    [Theory]
    [InlineData(TabStyleVariant.Underline, "tab-panel-underline")]
    [InlineData(TabStyleVariant.Pills, "tab-panel-pills")]
    [InlineData(TabStyleVariant.Bordered, "tab-panel-bordered")]
    [InlineData(TabStyleVariant.Portal, "tab-panel-portal")]
    public void StyleVariant_AppliesExpectedContainerClass(TabStyleVariant variant, string expectedClass)
    {
        var cut = Render<TabGroup>(p => p.Add(x => x.Tabs, ThreeTabs).Add(x => x.StyleVariant, variant));
        cut.Find("div.tab-panel-container").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void Compact_AddsCompactClass()
    {
        var cut = Render<TabGroup>(p => p.Add(x => x.Tabs, ThreeTabs).Add(x => x.Compact, true));
        cut.Find("div.tab-panel-container").ClassList.Should().Contain("tab-panel-compact");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<TabGroup>(p => p
            .Add(x => x.Tabs, ThreeTabs)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "my-tabgroup"));

        var root = cut.Find("div.tab-panel-container");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("my-tabgroup");
    }
}
