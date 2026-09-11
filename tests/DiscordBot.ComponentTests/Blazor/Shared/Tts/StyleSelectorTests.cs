using Microsoft.AspNetCore.Components;
using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Models;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Tts;

public class StyleSelectorTests : BlazorComponentTestContext
{
    private static readonly StyleOption[] Styles =
    {
        new() { Value = "", Label = "(None)" },
        new() { Value = "cheerful", Label = "Cheerful", Example = "Yay!" },
        new() { Value = "angry", Label = "Angry", Example = "Ugh!" },
    };

    [Fact]
    public void RendersOneOptionPerStyle_WithSelectedMarked()
    {
        var cut = Render<StyleSelector>(p => p.Add(x => x.AvailableStyles, Styles).Add(x => x.Value, "cheerful"));

        var options = cut.FindAll("option");
        options.Should().HaveCount(3);
        options.Single(o => o.GetAttribute("value") == "cheerful").HasAttribute("selected").Should().BeTrue();
    }

    [Fact]
    public void ChangingSelect_InvokesValueChanged()
    {
        string? changed = null;
        var cut = Render<StyleSelector>(p => p
            .Add(x => x.AvailableStyles, Styles)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => changed = v)));

        cut.Find("select").Change("angry");

        changed.Should().Be("angry");
    }

    [Fact]
    public void IntensitySlider_Input_InvokesIntensityChanged()
    {
        decimal? changed = null;
        var cut = Render<StyleSelector>(p => p
            .Add(x => x.AvailableStyles, Styles)
            .Add(x => x.Value, "cheerful")
            .Add(x => x.IntensityChanged, EventCallback.Factory.Create<decimal>(this, v => changed = v)));

        cut.Find("input[type=range]").Input("1.5");

        changed.Should().Be(1.5m);
    }

    [Fact]
    public void IntensitySlider_DisabledWhenNoStyleSelected()
    {
        var cut = Render<StyleSelector>(p => p.Add(x => x.AvailableStyles, Styles).Add(x => x.Value, ""));

        cut.Find("input[type=range]").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task CapabilitiesLoader_DisablesUnsupportedStyles_AndRaisesEmphasisSupportChanged()
    {
        bool? emphasisSupported = null;
        var cut = Render<StyleSelector>(p => p
            .Add(x => x.AvailableStyles, Styles)
            .Add(x => x.SelectedVoice, "en-US-GuyNeural")
            .Add(x => x.Value, "cheerful")
            .Add(x => x.CapabilitiesLoader, (Func<string, Task<VoiceCapabilities?>>)(_ => Task.FromResult<VoiceCapabilities?>(new VoiceCapabilities
            {
                VoiceName = "en-US-GuyNeural",
                DisplayName = "Guy",
                Locale = "en-US",
                Gender = "Male",
                SupportedStyles = new[] { "angry" },
                SupportsEmphasis = true
            })))
            .Add(x => x.OnEmphasisSupportChanged, EventCallback.Factory.Create<bool>(this, v => emphasisSupported = v)));

        await cut.InvokeAsync(() => Task.CompletedTask); // let OnParametersSetAsync's loader task settle

        emphasisSupported.Should().BeTrue();
        cut.FindAll("option").Single(o => o.GetAttribute("value") == "cheerful").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("option").Single(o => o.GetAttribute("value") == "angry").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task CapabilitiesLoader_NoSupportedStyles_ShowsNoStylesMessageAndResetsValue()
    {
        string? changedTo = "unset";
        var cut = Render<StyleSelector>(p => p
            .Add(x => x.AvailableStyles, Styles)
            .Add(x => x.SelectedVoice, "en-US-Unknown")
            .Add(x => x.Value, "cheerful")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => changedTo = v))
            .Add(x => x.CapabilitiesLoader, (Func<string, Task<VoiceCapabilities?>>)(_ => Task.FromResult<VoiceCapabilities?>(new VoiceCapabilities
            {
                VoiceName = "en-US-Unknown",
                DisplayName = "Unknown",
                Locale = "unknown",
                Gender = "Unknown",
                SupportedStyles = Array.Empty<string>(),
            }))));

        await cut.InvokeAsync(() => Task.CompletedTask);

        changedTo.Should().Be(string.Empty);
        cut.Find(".style-selector__no-styles-message").Should().NotBeNull();
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<StyleSelector>(p => p
            .Add(x => x.AvailableStyles, Styles)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "ss"));

        var root = cut.Find(".style-selector");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("ss");
    }
}
