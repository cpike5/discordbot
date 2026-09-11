using Bunit;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Tts;

public class EmphasisToolbarTests : BlazorComponentTestContext
{
    // BrowserInterop.GetSelectionAsync's Value is just the selected substring (browser.js's
    // getSelection: textarea.value.substring(selectionStart, selectionEnd)) - the full text comes
    // from the component's own Value *parameter*, set per-test below alongside Start/End.

    [Fact]
    public void RendersSixFormatButtons()
    {
        JSInterop.SetupModule("./js/blazor/browser.js");
        var cut = Render<EmphasisToolbar>();

        cut.FindAll(".emphasis-toolbar__button").Should().HaveCount(6);
        cut.FindAll(".emphasis-toolbar__separator").Should().HaveCount(3);
    }

    [Fact]
    public void EmphasisNotSupported_DisablesEmphasisButtons_AndShowsNotice()
    {
        JSInterop.SetupModule("./js/blazor/browser.js");
        var cut = Render<EmphasisToolbar>(p => p.Add(x => x.EmphasisSupported, false));

        cut.Find(".emphasis-toolbar__button--strong").ClassList.Should().Contain("emphasis-toolbar__button--emphasis-disabled");
        cut.Find(".emphasis-toolbar__button--moderate").ClassList.Should().Contain("emphasis-toolbar__button--emphasis-disabled");
        cut.Find(".emphasis-toolbar__notice").Should().NotBeNull();
    }

    [Fact]
    public void ShowKeyboardShortcuts_AddsHintsToTitles()
    {
        JSInterop.SetupModule("./js/blazor/browser.js");
        var cut = Render<EmphasisToolbar>(p => p.Add(x => x.ShowKeyboardShortcuts, true));

        cut.Find(".emphasis-toolbar__button--strong").GetAttribute("title").Should().Contain("Ctrl+B");
        cut.Find(".emphasis-toolbar__button--moderate").GetAttribute("title").Should().Contain("Ctrl+E");
    }

    [Fact]
    public void ClickStrong_NoSelection_DoesNothing()
    {
        var module = JSInterop.SetupModule("./js/blazor/browser.js");
        module.Setup<TextSelectionResult>("getSelection", _ => true)
            .SetResult(new TextSelectionResult(5, 5, string.Empty));
        var insertHandler = module.SetupVoid("insertAtSelection", _ => true);
        insertHandler.SetVoidResult();

        var changed = false;
        var cut = Render<EmphasisToolbar>(p => p
            .Add(x => x.Value, "no selection here")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, _ => changed = true)));

        cut.Find(".emphasis-toolbar__button--strong").Click();

        changed.Should().BeFalse();
        insertHandler.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void ClickStrong_WithSelection_WrapsSelectionAndInvokesValueChanged()
    {
        var module = JSInterop.SetupModule("./js/blazor/browser.js");
        module.Setup<TextSelectionResult>("getSelection", _ => true)
            .SetResult(new TextSelectionResult(6, 11, "world"));
        module.SetupVoid("setSelection", _ => true).SetVoidResult();
        var insertHandler = module.SetupVoid("insertAtSelection", _ => true);
        insertHandler.SetVoidResult();

        string? newValue = null;
        var cut = Render<EmphasisToolbar>(p => p
            .Add(x => x.Value, "hello world")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => newValue = v)));

        cut.Find(".emphasis-toolbar__button--strong").Click();

        newValue.Should().Be("hello **world**");
        insertHandler.Invocations.Should().ContainSingle(inv => (string)inv.Arguments[1]! == "**world**");
    }

    [Fact]
    public void ClickModerate_WrapsSelectionWithSingleAsterisks()
    {
        var module = JSInterop.SetupModule("./js/blazor/browser.js");
        module.Setup<TextSelectionResult>("getSelection", _ => true)
            .SetResult(new TextSelectionResult(0, 5, "hello"));
        module.SetupVoid("setSelection", _ => true).SetVoidResult();
        module.SetupVoid("insertAtSelection", _ => true).SetVoidResult();

        string? newValue = null;
        var cut = Render<EmphasisToolbar>(p => p
            .Add(x => x.Value, "hello")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => newValue = v)));

        // The moderate button is the second toolbar button (Strong, Moderate, Pause, #, date, Clear).
        cut.FindAll(".emphasis-toolbar__button")[1].Click();

        newValue.Should().Be("*hello*");
    }

    [Fact]
    public void ClickPause_WorksEvenWithCollapsedSelection()
    {
        var module = JSInterop.SetupModule("./js/blazor/browser.js");
        module.Setup<TextSelectionResult>("getSelection", _ => true)
            .SetResult(new TextSelectionResult(5, 5, string.Empty));
        module.SetupVoid("setSelection", _ => true).SetVoidResult();
        module.SetupVoid("insertAtSelection", _ => true).SetVoidResult();

        string? newValue = null;
        var cut = Render<EmphasisToolbar>(p => p
            .Add(x => x.Value, "hello world")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => newValue = v)));

        // Pause is the third rendered button.
        cut.FindAll(".emphasis-toolbar__button")[2].Click();

        newValue.Should().Be("hello[⏸️ 500ms] world");
    }

    [Fact]
    public void ClickClear_StripsMarkersFromSelection()
    {
        var module = JSInterop.SetupModule("./js/blazor/browser.js");
        module.Setup<TextSelectionResult>("getSelection", _ => true)
            .SetResult(new TextSelectionResult(0, 8, "**bold**"));
        module.SetupVoid("setSelection", _ => true).SetVoidResult();
        module.SetupVoid("insertAtSelection", _ => true).SetVoidResult();

        string? newValue = null;
        var cut = Render<EmphasisToolbar>(p => p
            .Add(x => x.Value, "**bold**")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => newValue = v)));

        // Clear is the last rendered button.
        cut.FindAll(".emphasis-toolbar__button")[^1].Click();

        newValue.Should().Be("bold");
    }

    [Fact]
    public async Task HandleShortcutAsync_B_AppliesStrongEmphasis()
    {
        var module = JSInterop.SetupModule("./js/blazor/browser.js");
        module.Setup<TextSelectionResult>("getSelection", _ => true)
            .SetResult(new TextSelectionResult(0, 4, "text"));
        module.SetupVoid("setSelection", _ => true).SetVoidResult();
        module.SetupVoid("insertAtSelection", _ => true).SetVoidResult();

        string? newValue = null;
        var cut = Render<EmphasisToolbar>(p => p
            .Add(x => x.Value, "text")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => newValue = v)));

        await cut.InvokeAsync(() => cut.Instance.HandleShortcutAsync("b"));

        newValue.Should().Be("**text**");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        JSInterop.SetupModule("./js/blazor/browser.js");
        var cut = Render<EmphasisToolbar>(p => p
            .Add(x => x.Class, "extra-class")
            .Add(x => x.TextareaId, "myTextarea")
            .AddUnmatched("data-testid", "et"));

        var root = cut.Find(".emphasis-toolbar");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-target").Should().Be("myTextarea");
        root.GetAttribute("data-testid").Should().Be("et");
    }
}
