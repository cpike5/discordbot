using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Shared;

public class TabPanelTests : TestContext
{
    private static TabPanelViewModel Model(string activeTabId = "") => new()
    {
        Id = "panelDemo",
        ActiveTabId = activeTabId,
        Tabs =
        [
            new TabItemViewModel { Id = "sounds", Label = "Soundboard", BadgeCount = 3 },
            new TabItemViewModel { Id = "tts", Label = "Text-to-Speech", ShortLabel = "TTS" },
        ],
    };

    private IRenderedComponent<TabPanel> Render(TabPanelViewModel model, Action<ComponentParameterCollectionBuilder<TabPanel>>? extra = null) =>
        RenderComponent<TabPanel>(p =>
        {
            p.Add(x => x.Model, model)
             .Add(x => x.TabContent, (string tabId) => (RenderFragment)(b => b.AddContent(0, $"panel:{tabId}")));
            extra?.Invoke(p);
        });

    [Fact]
    public void ActiveTabIdFromModel_IsHonored_AndOtherPanelHidden()
    {
        var cut = Render(Model("tts"));

        cut.Find("#panelDemo-tab-tts").ClassList.Should().Contain("active");
        cut.Find("#panelDemo-panel-tts").HasAttribute("hidden").Should().BeFalse();
        cut.Find("#panelDemo-panel-sounds").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public void Click_SwitchesTab_AndRaisesActiveTabChanged()
    {
        string? changedTo = null;
        var cut = Render(Model("sounds"), p => p.Add(x => x.ActiveTabChanged, (string id) => changedTo = id));

        cut.Find("#panelDemo-tab-tts").Click();

        changedTo.Should().Be("tts");
        cut.Instance.ActiveTab.Should().Be("tts");
        cut.Find("#panelDemo-tab-tts").GetAttribute("aria-selected").Should().Be("true");
        cut.Find("#panelDemo-panel-sounds").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public void RendersBadge_AndShortLabel()
    {
        var cut = Render(Model());

        cut.Find(".tab-badge").TextContent.Trim().Should().Be("3");
        cut.Find(".tab-label-short").TextContent.Trim().Should().Be("TTS");
    }

    [Fact]
    public void CompactAndVariant_AppliedToContainerClasses()
    {
        var model = Model() with { Compact = true, StyleVariant = TabStyleVariant.Pills };
        var cut = Render(model);

        var container = cut.Find("#panelDemo-container");
        container.ClassList.Should().Contain("tab-panel-pills").And.Contain("tab-panel-compact");
    }
}
