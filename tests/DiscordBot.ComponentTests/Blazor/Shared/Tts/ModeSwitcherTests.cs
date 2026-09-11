using Microsoft.AspNetCore.Components;
using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Tts;

public class ModeSwitcherTests : BlazorComponentTestContext
{
    [Fact]
    public void Default_RendersThreeTabs_ActiveTabHasAriaSelectedTrue()
    {
        JSInterop.SetupModule("./js/blazor/browser.js").Setup<string?>("storageGet", _ => true).SetResult(null);

        var cut = Render<ModeSwitcher>(p => p.Add(x => x.Value, TtsMode.Standard));

        var tabs = cut.FindAll("[role='tab']");
        tabs.Should().HaveCount(3);
        tabs.Single(t => t.TextContent.Contains("Standard")).GetAttribute("aria-selected").Should().Be("true");
        tabs.Where(t => !t.TextContent.Contains("Standard")).Should().OnlyContain(t => t.GetAttribute("aria-selected") == "false");
    }

    [Fact]
    public void ClickTab_InvokesValueChanged_AndPersistsToLocalStorage()
    {
        var module = JSInterop.SetupModule("./js/blazor/browser.js");
        module.Setup<string?>("storageGet", _ => true).SetResult(null);
        var setHandler = module.Setup<bool>("storageSet", _ => true);
        setHandler.SetResult(true);

        TtsMode? changed = null;
        var cut = Render<ModeSwitcher>(p => p
            .Add(x => x.Value, TtsMode.Standard)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<TtsMode>(this, v => changed = v)));

        cut.FindAll("[role='tab']").Single(t => t.TextContent.Contains("Pro")).Click();

        changed.Should().Be(TtsMode.Pro);
        setHandler.Invocations.Should().ContainSingle(inv => (string)inv.Arguments[0]! == "tts_mode_preference" && (string)inv.Arguments[1]! == "pro");
    }

    [Fact]
    public void OnAfterRender_RestoresSavedModeFromLocalStorage()
    {
        JSInterop.SetupModule("./js/blazor/browser.js").Setup<string?>("storageGet", _ => true).SetResult("pro");

        TtsMode? changed = null;
        var cut = Render<ModeSwitcher>(p => p
            .Add(x => x.Value, TtsMode.Standard)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<TtsMode>(this, v => changed = v)));

        cut.WaitForAssertion(() => changed.Should().Be(TtsMode.Pro));
    }

    [Fact]
    public void ArrowRight_MovesToNextMode()
    {
        var module = JSInterop.SetupModule("./js/blazor/browser.js");
        module.Setup<string?>("storageGet", _ => true).SetResult(null);
        module.Setup<bool>("storageSet", _ => true).SetResult(true);

        TtsMode? changed = null;
        var cut = Render<ModeSwitcher>(p => p
            .Add(x => x.Value, TtsMode.Simple)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<TtsMode>(this, v => changed = v)));

        cut.Find("[role='tab']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        changed.Should().Be(TtsMode.Standard);
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        JSInterop.SetupModule("./js/blazor/browser.js").Setup<string?>("storageGet", _ => true).SetResult(null);

        var cut = Render<ModeSwitcher>(p => p
            .Add(x => x.Value, TtsMode.Standard)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "ms"));

        var root = cut.Find("[role='tablist']");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("ms");
    }
}
