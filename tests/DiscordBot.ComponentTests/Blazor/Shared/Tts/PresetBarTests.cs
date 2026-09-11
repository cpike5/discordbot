using Microsoft.AspNetCore.Components;
using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;

namespace DiscordBot.ComponentTests.Blazor.Shared.Tts;

public class PresetBarTests : BlazorComponentTestContext
{
    private static readonly PresetButtonViewModel[] Presets =
    {
        new() { Id = "excited", Name = "Excited", Icon = "sparkles", VoiceName = "en-US-JennyNeural", Style = "cheerful", Speed = 1.2m, Pitch = 1.1m },
        new() { Id = "robot", Name = "Robot", Icon = "computer-desktop", VoiceName = "en-US-GuyNeural", Speed = 0.9m, Pitch = 0.7m },
    };

    [Fact]
    public void RendersOneButtonPerPreset_PlusSaveButton()
    {
        var cut = Render<PresetBar>(p => p.Add(x => x.Presets, Presets));

        var buttons = cut.FindAll(".preset-button");
        buttons.Should().HaveCount(3); // 2 presets + Save
        cut.Find(".preset-button--save").TextContent.Should().Contain("Save");
    }

    [Fact]
    public void ActivePresetId_MarksMatchingButtonActiveWithBadge()
    {
        var cut = Render<PresetBar>(p => p.Add(x => x.Presets, Presets).Add(x => x.ActivePresetId, "robot"));

        var robotButton = cut.FindAll(".preset-button").First(b => b.TextContent.Contains("Robot"));
        robotButton.ClassList.Should().Contain("preset-button--active");
        robotButton.QuerySelector(".preset-button__active-badge").Should().NotBeNull();
    }

    [Fact]
    public void ClickPreset_InvokesOnApplyWithThatPreset()
    {
        PresetButtonViewModel? applied = null;
        var cut = Render<PresetBar>(p => p
            .Add(x => x.Presets, Presets)
            .Add(x => x.OnApply, EventCallback.Factory.Create<PresetButtonViewModel>(this, v => applied = v)));

        cut.FindAll(".preset-button").First(b => b.TextContent.Contains("Excited")).Click();

        applied.Should().NotBeNull();
        applied!.Id.Should().Be("excited");
    }

    [Fact]
    public async Task ClickSave_RevealsInlineNameField_ConfirmInvokesOnSaveCustomWithTrimmedName()
    {
        string? saved = null;
        var cut = Render<PresetBar>(p => p
            .Add(x => x.Presets, Presets)
            .Add(x => x.OnSaveCustom, EventCallback.Factory.Create<string>(this, v => saved = v)));

        await cut.InvokeAsync(() => cut.Find(".preset-button--save").Click());
        await cut.InvokeAsync(() => cut.Find(".preset-button__save-input").Input("  My Preset  "));
        await cut.InvokeAsync(() => cut.Find(".preset-button__save-confirm").Click());

        saved.Should().Be("My Preset");
        cut.FindAll(".preset-button__save-input").Should().BeEmpty();
    }

    [Fact]
    public async Task ClickSave_ThenCancel_DoesNotInvokeOnSaveCustom()
    {
        var invoked = false;
        var cut = Render<PresetBar>(p => p
            .Add(x => x.Presets, Presets)
            .Add(x => x.OnSaveCustom, EventCallback.Factory.Create<string>(this, () => invoked = true)));

        await cut.InvokeAsync(() => cut.Find(".preset-button--save").Click());
        await cut.InvokeAsync(() => cut.Find(".preset-button__save-cancel").Click());

        invoked.Should().BeFalse();
        cut.FindAll(".preset-button--save").Should().ContainSingle();
    }

    [Fact]
    public void CustomPresets_RenderWithCustomBadgeAndDeleteButton()
    {
        var customs = new[] { new CustomTtsPreset(1, "My Robot", "en-US-GuyNeural", null, 1.0m, 1.0m) };
        var cut = Render<PresetBar>(p => p.Add(x => x.Presets, Presets).Add(x => x.CustomPresets, customs));

        var slot = cut.Find(".preset-button-slot");
        slot.QuerySelector(".preset-button__badge")!.TextContent.Should().Be("Custom");
        slot.QuerySelector(".preset-button__delete").Should().NotBeNull();
    }

    [Fact]
    public void ClickCustomPreset_InvokesOnApplyWithSynthesizedCustomId()
    {
        PresetButtonViewModel? applied = null;
        var customs = new[] { new CustomTtsPreset(7, "My Robot", "en-US-GuyNeural", "angry", 1.0m, 1.0m) };
        var cut = Render<PresetBar>(p => p
            .Add(x => x.Presets, Presets)
            .Add(x => x.CustomPresets, customs)
            .Add(x => x.OnApply, EventCallback.Factory.Create<PresetButtonViewModel>(this, v => applied = v)));

        cut.Find(".preset-button--custom").Click();

        applied.Should().NotBeNull();
        applied!.Id.Should().Be("custom-7");
        applied.VoiceName.Should().Be("en-US-GuyNeural");
        applied.Style.Should().Be("angry");
    }

    [Fact]
    public void DeleteButton_RequiresTwoClicks_ThenInvokesOnDeleteCustom()
    {
        CustomTtsPreset? deleted = null;
        var customs = new[] { new CustomTtsPreset(3, "Temp", "en-US-JennyNeural", null, 1.0m, 1.0m) };
        var cut = Render<PresetBar>(p => p
            .Add(x => x.Presets, Presets)
            .Add(x => x.CustomPresets, customs)
            .Add(x => x.OnDeleteCustom, EventCallback.Factory.Create<CustomTtsPreset>(this, v => deleted = v)));

        cut.Find(".preset-button__delete").Click();
        deleted.Should().BeNull();
        cut.Find(".preset-button__delete").ClassList.Should().Contain("preset-button__delete--confirm");

        cut.Find(".preset-button__delete").Click();

        deleted.Should().NotBeNull();
        deleted!.Id.Should().Be(3);
    }

    [Fact]
    public void ArrowRight_MovesToNextPreset_AndInvokesOnApply()
    {
        PresetButtonViewModel? applied = null;
        var cut = Render<PresetBar>(p => p
            .Add(x => x.Presets, Presets)
            .Add(x => x.ActivePresetId, "excited")
            .Add(x => x.OnApply, EventCallback.Factory.Create<PresetButtonViewModel>(this, v => applied = v)));

        cut.Find(".presets-bar").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        applied.Should().NotBeNull();
        applied!.Id.Should().Be("robot");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<PresetBar>(p => p
            .Add(x => x.Presets, Presets)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "pb"));

        var root = cut.Find(".presets-bar-wrapper");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("pb");
    }
}
