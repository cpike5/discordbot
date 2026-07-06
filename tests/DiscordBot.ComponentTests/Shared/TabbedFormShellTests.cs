using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Shared;

public class TabbedFormShellTests : TestContext
{
    private static readonly IReadOnlyList<TabDefinition> Tabs = new[]
    {
        new TabDefinition("overview", "Overview"),
        new TabDefinition("spam", "Spam"),
    };

    public TabbedFormShellTests()
    {
        // TabbedFormShell arms/disarms blazorInterop.setUnsavedGuard over JS interop.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<TabbedFormShell> Render() =>
        RenderComponent<TabbedFormShell>(p => p
            .Add(x => x.Tabs, Tabs)
            .Add(x => x.TabContent, (string tabId) => (RenderFragment)(b => b.AddContent(0, $"panel:{tabId}"))));

    [Fact]
    public void FirstTabIsActive_AndAllPanelsRendered()
    {
        var cut = Render();

        cut.Find("#tab-btn-overview").ClassList.Should().Contain("settings-tab-active");
        cut.Find("#tab-overview").ClassList.Should().NotContain("hidden");
        cut.Find("#tab-spam").ClassList.Should().Contain("hidden");
        cut.Markup.Should().Contain("panel:overview").And.Contain("panel:spam");
    }

    [Fact]
    public void TabSwitch_WhenClean_SwitchesImmediately()
    {
        var cut = Render();

        cut.Find("#tab-btn-spam").Click();

        cut.Instance.ActiveTab.Should().Be("spam");
        cut.Find("#tab-spam").ClassList.Should().NotContain("hidden");
        cut.Find("#tab-overview").ClassList.Should().Contain("hidden");
    }

    [Fact]
    public async Task TabSwitch_WhenDirty_ShowsGuard_AndStaysOnCancel()
    {
        var cut = Render();
        await cut.InvokeAsync(() => cut.Instance.MarkDirtyAsync());

        // The click handler awaits the guard modal, so hold the task un-awaited
        // until the modal is dismissed (a sync Click() would deadlock).
        var switchTask = cut.Find("#tab-btn-spam").ClickAsync(new MouseEventArgs());

        // Guard modal is up; cancel keeps the current tab and the dirty flag.
        var dialog = cut.Find("[role=alertdialog]");
        dialog.TextContent.Should().Contain("Unsaved Changes");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Stay").Click();
        await switchTask;

        cut.Instance.ActiveTab.Should().Be("overview");
        cut.Instance.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task TabSwitch_WhenDirty_SwitchesOnConfirm_AndResetsDirty()
    {
        var cut = Render();
        await cut.InvokeAsync(() => cut.Instance.MarkDirtyAsync());

        var switchTask = cut.Find("#tab-btn-spam").ClickAsync(new MouseEventArgs());
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Switch Tab").Click();
        await switchTask;

        cut.Instance.ActiveTab.Should().Be("spam");
        cut.Instance.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task DirtyFlag_ArmsAndDisarmsBrowserGuard()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var arm = JSInterop.SetupVoid("blazorInterop.setUnsavedGuard", true);
        var disarm = JSInterop.SetupVoid("blazorInterop.setUnsavedGuard", false);
        arm.SetVoidResult();
        disarm.SetVoidResult();

        var cut = Render();
        await cut.InvokeAsync(() => cut.Instance.MarkDirtyAsync());
        arm.Invocations.Should().HaveCount(1);

        await cut.InvokeAsync(() => cut.Instance.MarkCleanAsync());
        disarm.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public void InitialTab_Honored()
    {
        var cut = RenderComponent<TabbedFormShell>(p => p
            .Add(x => x.Tabs, Tabs)
            .Add(x => x.InitialTab, "spam")
            .Add(x => x.TabContent, (string tabId) => (RenderFragment)(b => b.AddContent(0, tabId))));

        cut.Instance.ActiveTab.Should().Be("spam");
    }
}
