using Microsoft.AspNetCore.Components;
using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Tts;

public class VoiceSelectorTests : BlazorComponentTestContext
{
    private static readonly VoiceSelectorVoiceOption[] Voices =
    {
        new() { Value = "en-US-JennyNeural", DisplayName = "Jenny", Gender = "Female", Locale = "en-US", LocaleDisplayName = "English (US)" },
        new() { Value = "en-US-GuyNeural", DisplayName = "Guy", Gender = "Male", Locale = "en-US", LocaleDisplayName = "English (US)" },
        new() { Value = "fr-FR-DeniseNeural", DisplayName = "Denise", Gender = "Female", Locale = "fr-FR", LocaleDisplayName = "French (France)" },
    };

    [Fact]
    public void Default_ClosedWithPlaceholderTriggerText()
    {
        var cut = Render<VoiceSelector>(p => p.Add(x => x.Voices, Voices).Add(x => x.Placeholder, "Pick a voice"));

        cut.Find(".voice-selector__trigger-text").TextContent.Should().Be("Pick a voice");
        cut.Find(".voice-selector__trigger").GetAttribute("aria-expanded").Should().Be("false");
        cut.FindAll(".voice-selector__dropdown").Should().BeEmpty();
    }

    [Fact]
    public void SelectedValue_ShowsFormattedTriggerText()
    {
        var cut = Render<VoiceSelector>(p => p.Add(x => x.Voices, Voices).Add(x => x.Value, "en-US-JennyNeural"));

        cut.Find(".voice-selector__trigger-text").TextContent.Should().Be("Jenny (Female) - English (US)");
    }

    [Fact]
    public async Task ClickTrigger_OpensDropdown_GroupedByLocale()
    {
        var cut = Render<VoiceSelector>(p => p.Add(x => x.Voices, Voices));

        await cut.InvokeAsync(() => cut.Find(".voice-selector__trigger").Click());

        cut.Find(".voice-selector__dropdown").Should().NotBeNull();
        cut.FindAll(".voice-selector__group").Should().HaveCount(2);
        cut.Find(".voice-selector__trigger").GetAttribute("aria-expanded").Should().Be("true");
    }

    [Fact]
    public async Task ClickOption_InvokesValueChanged_AndClosesDropdown()
    {
        string? selected = null;
        var cut = Render<VoiceSelector>(p => p
            .Add(x => x.Voices, Voices)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => selected = v)));

        await cut.InvokeAsync(() => cut.Find(".voice-selector__trigger").Click());
        await cut.InvokeAsync(() => cut.Find(".voice-selector__option").Click());

        selected.Should().NotBeNull();
        cut.FindAll(".voice-selector__dropdown").Should().BeEmpty();
    }

    [Fact]
    public async Task Search_FiltersToMatchingVoicesOnly()
    {
        var cut = Render<VoiceSelector>(p => p.Add(x => x.Voices, Voices));
        await cut.InvokeAsync(() => cut.Find(".voice-selector__trigger").Click());

        await cut.InvokeAsync(() => cut.Find(".voice-selector__search").Input("denise"));

        var visibleOptions = cut.FindAll(".voice-selector__option")
            .Where(e => !e.ClassList.Contains("voice-selector__option--hidden"))
            .ToList();
        visibleOptions.Should().ContainSingle();
        visibleOptions[0].TextContent.Should().Contain("Denise");
    }

    [Fact]
    public async Task Search_NoMatches_ShowsNoVoicesFoundMessage()
    {
        var cut = Render<VoiceSelector>(p => p.Add(x => x.Voices, Voices));
        await cut.InvokeAsync(() => cut.Find(".voice-selector__trigger").Click());

        await cut.InvokeAsync(() => cut.Find(".voice-selector__search").Input("zzz-no-match"));

        cut.Find(".voice-selector__empty").TextContent.Should().Be("No voices found");
    }

    [Fact]
    public async Task NoVoicesAtAll_ShowsEmptyMessageWhenOpened()
    {
        var cut = Render<VoiceSelector>(p => p.Add(x => x.Voices, Array.Empty<VoiceSelectorVoiceOption>()));

        await cut.InvokeAsync(() => cut.Find(".voice-selector__trigger").Click());

        cut.Find(".voice-selector__empty").TextContent.Should().Be("No voices available");
    }

    [Fact]
    public async Task GroupHeaderClick_TogglesGroupExpanded()
    {
        var cut = Render<VoiceSelector>(p => p.Add(x => x.Voices, Voices));
        await cut.InvokeAsync(() => cut.Find(".voice-selector__trigger").Click());
        cut.Find(".voice-selector__group-header").GetAttribute("aria-expanded").Should().Be("true");

        await cut.InvokeAsync(() => cut.Find(".voice-selector__group-header").Click());

        cut.Find(".voice-selector__group-header").GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<VoiceSelector>(p => p
            .Add(x => x.Voices, Voices)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "vs"));

        var root = cut.Find(".voice-selector");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("vs");
    }
}
